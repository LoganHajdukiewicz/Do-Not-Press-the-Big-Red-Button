using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    /// <summary>
    /// Guards the day object against components that outlive or delete their scene.
    /// Attaching the music to the shared "Day" object once destroyed DayLevel and
    /// TimedReveal with it, which stopped buttons resolving and stopped Day 3's
    /// green button appearing. These tests fail if that returns.
    /// </summary>
    public sealed class DayRegressionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<AudioClip> clips = new List<AudioClip>();
        private readonly List<string> loaded = new List<string>();

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            BackgroundMusic.ClearPersistent();
            DayFlow.ResetForTests();
            GameSettings.ResetForTests();
            loaded.Clear();
            DayFlow.SceneLoader = name => loaded.Add(name);
            DayFlow.SceneExists = _ => true;
        }

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
            DayFlow.ResetForTests();
            GameSettings.ResetForTests();
        }

        private AudioClip Clip(string name)
        {
            AudioClip clip = AudioClip.Create(name, 44100, 1, 44100, false);
            clips.Add(clip);
            return clip;
        }

        /// <summary>A realistic day object: the same components the saved scenes carry.</summary>
        private (GameObject host, DayLevel day, BackgroundMusic music) BuildDayObject(
            int dayNumber, AudioClip music, bool withReveal = false)
        {
            var host = new GameObject("Day");
            objects.Add(host);
            host.AddComponent<DayTitle>();
            var day = host.AddComponent<DayLevel>();
            Set(day, "dayNumber", dayNumber);
            Set(day, "delayBeforeNextDay", 0f);

            TimedReveal reveal = null;
            GameObject hidden = null;
            if (withReveal)
            {
                hidden = new GameObject("Green Button assembly");
                objects.Add(hidden);
                reveal = host.AddComponent<TimedReveal>();
                Set(reveal, "target", hidden);
                Set(reveal, "delay", 0.15f);
            }

            var track = host.AddComponent<BackgroundMusic>();
            Set(track, "music", music);
            Set(track, "fadeInDuration", 0f);
            Set(track, "crossFadeDuration", 0f);
            return (host, day, track);
        }

        [UnityTest]
        public IEnumerator MusicNeverCarriesTheDayObjectBetweenDays()
        {
            AudioClip corporate = Clip("Corporate");
            var (host, day, _) = BuildDayObject(1, corporate);
            yield return null;

            Assert.That(host.scene.name, Is.Not.EqualTo("DontDestroyOnLoad"),
                "The Day object must stay in its scene, or a stale DayLevel survives " +
                "into the next day and the new day's buttons resolve nothing.");
            Assert.That(day != null, Is.True, "DayLevel must still exist.");
            Assert.That(BackgroundMusic.Active, Is.Not.Null);
            Assert.That(BackgroundMusic.Active.gameObject, Is.Not.SameAs(host),
                "The persistent music lives on its own object, not the day's.");
        }

        [UnityTest]
        public IEnumerator SecondDayKeepsItsOwnDayLevelAndRevealedButton()
        {
            AudioClip corporate = Clip("Corporate");
            BuildDayObject(1, corporate);
            yield return null;

            // Day 3 arrives while the music is already running.
            var (host3, day3, _) = BuildDayObject(3, corporate, withReveal: true);
            yield return null;

            Assert.That(host3 != null, Is.True,
                "A later day's object must not be destroyed by the music hand-over.");
            Assert.That(day3 != null, Is.True, "Its DayLevel must survive.");
            Assert.That(host3.GetComponent<TimedReveal>(), Is.Not.Null,
                "TimedReveal must survive, or Day 3's green button never appears.");
        }

        [UnityTest]
        public IEnumerator DayThreeGreenButtonAppearsAfterTheDelay()
        {
            AudioClip corporate = Clip("Corporate");
            BuildDayObject(1, corporate);
            yield return null;

            var (host, _, _) = BuildDayObject(3, corporate, withReveal: true);
            var reveal = host.GetComponent<TimedReveal>();
            GameObject hidden = (GameObject)typeof(TimedReveal)
                .GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(reveal);

            yield return null;
            Assert.That(hidden.activeSelf, Is.False, "Hidden for the first few seconds.");
            yield return new WaitForSeconds(0.3f);
            Assert.That(hidden.activeSelf, Is.True, "The green button must spawn in.");
        }

        [UnityTest]
        public IEnumerator GreenButtonStillAdvancesOnALaterDay()
        {
            AudioClip corporate = Clip("Corporate");
            BuildDayObject(1, corporate);
            yield return null;

            var (_, day, _) = BuildDayObject(7, corporate);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            objects.Add(cap);
            var button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            button.OnPressed.AddListener(day.CompleteDay);
            yield return null;

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 8" }),
                "The green button must move the player forward on every day.");
        }

        [UnityTest]
        public IEnumerator RedButtonStillRestartsOnALaterDay()
        {
            AudioClip corporate = Clip("Corporate");
            BuildDayObject(1, corporate);
            yield return null;

            var (_, day, _) = BuildDayObject(7, corporate);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            objects.Add(cap);
            var button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            button.OnPressed.AddListener(day.FailDay);
            yield return null;

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 7" }),
                "The red button must restart the day.");
        }

        [UnityTest]
        public IEnumerator OnlyOneDayLevelExistsAfterSeveralDays()
        {
            AudioClip corporate = Clip("Corporate");
            var (host1, _, _) = BuildDayObject(1, corporate);
            yield return null;

            // Simulate the scene change: the old day's objects go away.
            Object.DestroyImmediate(host1);
            objects.Remove(host1);
            var (_, day2, _) = BuildDayObject(2, corporate);
            yield return null;

            DayLevel[] levels = Object.FindObjectsByType<DayLevel>(FindObjectsSortMode.None);
            Assert.That(levels.Length, Is.EqualTo(1),
                "Two DayLevels would make buttons resolve against the wrong day.");
            Assert.That(levels[0], Is.SameAs(day2));
            Assert.That(BackgroundMusic.Active, Is.Not.Null,
                "The music still survives the scene change.");
        }

        [UnityTest]
        public IEnumerator MusicKeepsPlayingWhenADayObjectIsDestroyed()
        {
            AudioClip corporate = Clip("Corporate");
            var (host1, _, _) = BuildDayObject(1, corporate);
            yield return null;
            BackgroundMusic persistent = BackgroundMusic.Active;
            AudioSource source = persistent.GetComponent<AudioSource>();
            source.time = 4f;

            Object.DestroyImmediate(host1);
            objects.Remove(host1);
            yield return null;

            Assert.That(BackgroundMusic.Active, Is.SameAs(persistent),
                "Unloading a day must not clear the shared music slot.");
            Assert.That(source != null && source.isPlaying, Is.True,
                "The track keeps playing across the day change.");

            BuildDayObject(2, corporate);
            yield return null;
            Assert.That(source.time, Is.GreaterThanOrEqualTo(4f),
                "The next day continues the track instead of restarting it.");
        }

        [UnityTest]
        public IEnumerator OpeningSequenceOnDayOneStillReleasesThePlayer()
        {
            AudioClip corporate = Clip("Corporate");
            var (host, _, _) = BuildDayObject(1, corporate);

            // The opening shares the Day object in the real Day 1 scene.
            var standIn = new GameObject("Player", typeof(FirstPersonHUD));
            objects.Add(standIn);
            var opening = host.AddComponent<OpeningSequence>();
            Set(opening, "frozenDuringOpening", standIn.GetComponent<FirstPersonHUD>());
            Set(opening, "openingNarration", null);
            Set(opening, "fallbackNarrationLength", 0.1f);
            Set(opening, "holdAfterNarration", 0f);
            Set(opening, "fadeOutDuration", 0f);
            yield return null;

            Assert.That(host.GetComponent<OpeningSequence>(), Is.Not.Null,
                "The opening must survive alongside the music.");
            yield return new WaitForSeconds(0.35f);
            Assert.That(opening.IsRunning, Is.False, "The opening finishes normally.");
            Assert.That(standIn.GetComponent<FirstPersonHUD>().enabled, Is.True,
                "Control returns to the player after the opening.");
        }
    }
}
