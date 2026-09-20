using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    /// <summary>
    /// The corporate track loops across the working days without restarting when a
    /// new day scene loads.
    /// </summary>
    public sealed class BackgroundMusicTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<AudioClip> clips = new List<AudioClip>();

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private AudioClip Clip(string name) =>
            Track(AudioClip.Create(name, 44100 * 2, 1, 44100, false));

        private AudioClip Track(AudioClip clip)
        {
            clips.Add(clip);
            return clip;
        }

        /// <summary>A day arriving with its own music component, as each scene has.</summary>
        private BackgroundMusic NewDay(AudioClip clip, float fade = 0f, float delay = 0f)
        {
            var host = new GameObject("Day");
            objects.Add(host);
            var music = host.AddComponent<BackgroundMusic>();
            // Configure before Awake by disabling, so the test controls the values.
            Set(music, "music", clip);
            Set(music, "fadeInDuration", fade);
            Set(music, "crossFadeDuration", fade);
            Set(music, "startDelay", delay);
            // Long pass, no silence: these tests are about persistence across days,
            // not the fade rhythm, so keep playback continuous and predictable.
            Set(music, "fadeOutDuration", 0f);
            Set(music, "silenceBetweenLoops", 0f);
            return music;
        }

        [SetUp]
        public void SetUp() => BackgroundMusic.ClearPersistent();

        [TearDown]
        public void TearDown()
        {
            BackgroundMusic.ClearPersistent();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
            foreach (AudioClip clip in clips)
                if (clip != null)
                    Object.DestroyImmediate(clip);
            clips.Clear();
        }

        [UnityTest]
        public IEnumerator EachPassRisesFromSilenceAndSinksBackIntoIt()
        {
            // A short clip with quick fades, so a whole pass fits in a test.
            AudioClip corporate = Track(AudioClip.Create("Short", 44100, 1, 44100, false));
            var host = new GameObject("Day");
            objects.Add(host);
            var music = host.AddComponent<BackgroundMusic>();
            Set(music, "music", corporate);
            Set(music, "loop", true);
            Set(music, "fadeInDuration", 0.2f);
            Set(music, "fadeOutDuration", 0.2f);
            Set(music, "silenceBetweenLoops", 0.3f);
            yield return null;

            AudioSource source = BackgroundMusic.Active.GetComponent<AudioSource>();
            Assert.That(source.loop, Is.False,
                "Unity's own loop flag would restart at full volume; passes are driven manually.");

            // Starts from silence, not at full volume.
            Assert.That(source.volume, Is.LessThan(0.1f), "A pass begins silent.");
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(source.volume, Is.EqualTo(music.TargetVolume).Within(0.02f),
                "It reaches full volume after fading in.");

            // Then sinks back to silence at the end of the pass.
            yield return new WaitForSecondsRealtime(0.85f);
            Assert.That(source.volume, Is.LessThan(0.1f),
                "The pass fades back down to silence rather than cutting off.");
        }

        [UnityTest]
        public IEnumerator ThePassRepeatsAfterItsSilence()
        {
            AudioClip corporate = Track(AudioClip.Create("Short", 44100, 1, 44100, false));
            var host = new GameObject("Day");
            objects.Add(host);
            var music = host.AddComponent<BackgroundMusic>();
            Set(music, "music", corporate);
            Set(music, "loop", true);
            Set(music, "fadeInDuration", 0.1f);
            Set(music, "fadeOutDuration", 0.1f);
            Set(music, "silenceBetweenLoops", 0.2f);
            yield return null;

            BackgroundMusic player = BackgroundMusic.Active;
            Assert.That(player.LoopsPlayed, Is.EqualTo(1), "The first pass has begun.");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(player.LoopsPlayed, Is.GreaterThan(1),
                "After the silence, the track comes back rather than stopping for good.");
        }

        [UnityTest]
        public IEnumerator ASinglePassStaysUpWhenLoopingIsOff()
        {
            AudioClip jingle = Clip("Once");
            var host = new GameObject("Day");
            objects.Add(host);
            var music = host.AddComponent<BackgroundMusic>();
            Set(music, "music", jingle);
            Set(music, "loop", false);
            Set(music, "fadeInDuration", 0.1f);
            yield return null;

            yield return new WaitForSecondsRealtime(0.25f);
            AudioSource source = BackgroundMusic.Active.GetComponent<AudioSource>();
            Assert.That(source.volume, Is.EqualTo(music.TargetVolume).Within(0.02f),
                "With looping off it fades up once and holds, as before.");
            Assert.That(BackgroundMusic.Active.LoopsPlayed, Is.Zero,
                "No pass counting when it is not looping.");
        }

        [UnityTest]
        public IEnumerator MusicStartsAndLoops()
        {
            AudioClip corporate = Clip("Corporate");
            BackgroundMusic day1 = NewDay(corporate);
            yield return null;

            Assert.That(BackgroundMusic.Active, Is.Not.Null, "The first day owns the music.");
            AudioSource source = BackgroundMusic.Active.GetComponent<AudioSource>();
            Assert.That(source.clip, Is.SameAs(corporate));
            Assert.That(source.loop, Is.False,
                "Looping is driven by the pass coroutine, not Unity's loop flag, so each " +
                "pass can fade up from silence and back down again.");
            Assert.That(BackgroundMusic.Active.LoopsPlayed, Is.GreaterThanOrEqualTo(1),
                "The first pass has started.");
            Assert.That(source.spatialBlend, Is.Zero, "Music is not positional.");
            Assert.That(day1, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LoadingTheNextDayDoesNotRestartTheSameTrack()
        {
            AudioClip corporate = Clip("Corporate");
            NewDay(corporate);
            yield return null;

            BackgroundMusic persistent = BackgroundMusic.Active;
            AudioSource source = persistent.GetComponent<AudioSource>();

            // Let the track get somewhere, then a new day arrives with the same clip.
            // Stays inside the 2 second test clip, so the position is valid.
            source.time = 1f;
            float before = source.time;
            BackgroundMusic day2 = NewDay(corporate);
            yield return null;

            Assert.That(BackgroundMusic.Active, Is.SameAs(persistent),
                "The running player keeps the music; the new day's copy stands down.");
            Assert.That(day2 != null, Is.True,
                "The new day's component must survive: it shares the Day object with " +
                "DayLevel, so destroying it would delete the day's own logic.");
            Assert.That(day2.gameObject.GetComponent<AudioSource>(), Is.Null,
                "A stood-down day adds no second audio source.");
            Assert.That(source.clip, Is.SameAs(corporate));
            Assert.That(source.time, Is.GreaterThanOrEqualTo(before),
                "Playback must continue, not rewind to the start of the day.");
        }

        [UnityTest]
        public IEnumerator ThirtyDaysInARowKeepOnePlayerAndOneTrack()
        {
            AudioClip corporate = Clip("Corporate");
            NewDay(corporate);
            yield return null;
            BackgroundMusic persistent = BackgroundMusic.Active;
            persistent.GetComponent<AudioSource>().time = 0.5f;

            for (int day = 2; day <= 30; day++)
            {
                NewDay(corporate);
                yield return null;
                Assert.That(BackgroundMusic.Active, Is.SameAs(persistent), $"Day {day}");
            }

            Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1), "Thirty days must not stack thirty audio sources.");
            Assert.That(persistent.GetComponent<AudioSource>().time,
                Is.GreaterThanOrEqualTo(0.5f), "The same playback position carried through.");
        }

        [UnityTest]
        public IEnumerator MusicSurvivesItsOwnDayObjectBeingDestroyed()
        {
            AudioClip corporate = Clip("Corporate");
            BackgroundMusic day1 = NewDay(corporate);
            yield return null;

            // The music runs on its own object, so unloading a scene cannot take it
            // with it, and the day object is left exactly where it was.
            Assert.That(BackgroundMusic.Active.gameObject, Is.Not.SameAs(day1.gameObject));
            Assert.That(BackgroundMusic.Active.gameObject.name, Is.EqualTo("Background Music"));
            Assert.That(BackgroundMusic.Active.gameObject.scene.name,
                Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(day1.gameObject.scene.name, Is.Not.EqualTo("DontDestroyOnLoad"),
                "The day object must never be carried between scenes.");
        }

        [UnityTest]
        public IEnumerator ADayWithNoClipFadesTheCorporateTrackOut()
        {
            AudioClip corporate = Clip("Corporate");
            NewDay(corporate);
            yield return null;
            AudioSource source = BackgroundMusic.Active.GetComponent<AudioSource>();
            Assert.That(source.isPlaying, Is.True);

            // Day 31 has an empty slot until its own music is added.
            NewDay(null);
            yield return null;
            yield return null;
            Assert.That(source.isPlaying, Is.False,
                "The working-day music must not play over the ending.");
        }

        [UnityTest]
        public IEnumerator ADifferentClipCrossFadesInsteadOfStacking()
        {
            AudioClip corporate = Clip("Corporate");
            AudioClip happier = Clip("Happier");
            NewDay(corporate);
            yield return null;
            AudioSource source = BackgroundMusic.Active.GetComponent<AudioSource>();

            NewDay(happier);
            yield return null;
            yield return null;
            Assert.That(source.clip, Is.SameAs(happier), "The new track replaces the old one.");
            Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Length, Is.EqualTo(1), "One source, not two playing at once.");
        }

        [UnityTest]
        public IEnumerator ReturningToTheMenuRestartsTheTrackNextTime()
        {
            AudioClip corporate = Clip("Corporate");
            NewDay(corporate);
            yield return null;
            BackgroundMusic.Active.GetComponent<AudioSource>().time = 1f;

            // The start menu clears the player, so a new month begins from the top.
            BackgroundMusic.ClearPersistent();
            Assert.That(BackgroundMusic.Active, Is.Null);

            NewDay(corporate);
            yield return null;
            Assert.That(BackgroundMusic.Active, Is.Not.Null);
            Assert.That(BackgroundMusic.Active.GetComponent<AudioSource>().time,
                Is.LessThan(1f), "A fresh run starts the corporate track from the beginning.");
        }

        [UnityTest]
        public IEnumerator DayOneWaitsForTheNarrationBeforePlaying()
        {
            AudioClip corporate = Clip("Corporate");
            NewDay(corporate, delay: 30f);
            yield return null;
            yield return null;

            AudioSource source = BackgroundMusic.Active.GetComponent<AudioSource>();
            Assert.That(source.isPlaying, Is.False,
                "The music must not talk over the opening narration.");
        }

        [UnityTest]
        public IEnumerator SceneOnlyMusicDoesNotPersist()
        {
            AudioClip jingle = Clip("Scene only");
            var host = new GameObject("Scene music");
            objects.Add(host);
            var music = host.AddComponent<BackgroundMusic>();
            Set(music, "music", jingle);
            Set(music, "continueAcrossDays", false);
            Set(music, "fadeInDuration", 0f);
            yield return null;

            Assert.That(BackgroundMusic.Active, Is.Null, "It never claims the shared slot.");
            Assert.That(music.gameObject.scene.name, Is.Not.EqualTo("DontDestroyOnLoad"));
            Assert.That(music.GetComponent<AudioSource>(), Is.Not.Null,
                "Scene-only music plays on its own object.");
        }
    }
}
