using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BorderValley.Battle.Domain;
using BorderValley.Core;
using BorderValley.Data;
using BorderValley.Data.World;
using BorderValley.Presentation;
using BorderValley.UI.Battle;
using BorderValley.UI.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BorderValley.PlayModeTests
{
    public sealed class PresentationFlowTests
    {
        [UnityTest]
        public IEnumerator PlayerMovingRight_UsesWalkEastClip()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            Object.FindAnyObjectByType<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.PlayerAnimator, Is.Not.Null);
            controller.Joystick.SetValue(Vector2.right);
            yield return null;

            Assert.That(
                controller.PlayerAnimator.CurrentClipId,
                Is.EqualTo("world.player.east.walk"));
        }

        [UnityTest]
        public IEnumerator PlayerStopped_AfterMovingEast_UsesIdleEastClip()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            Object.FindAnyObjectByType<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            controller.Joystick.SetValue(Vector2.right);
            yield return null;
            controller.Joystick.SetValue(Vector2.zero);
            yield return null;

            Assert.That(
                controller.PlayerAnimator.CurrentClipId,
                Is.EqualTo("world.player.east.idle"));
        }

        [UnityTest]
        public IEnumerator MenuAndWorldStart_ChangesMusicCue()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;

            var recording = WrapPresentation();
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Assert.That(recording.MusicCalls, Does.Contain("bgm.menu"));

            Object.FindAnyObjectByType<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            Assert.That(recording.MusicCalls, Does.Contain("bgm.world.village"));
        }

        [UnityTest]
        public IEnumerator AreaTransition_ChangesMusicCue()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            var recording = WrapPresentation();
            Object.FindAnyObjectByType<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;
            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            var exit = FindAreaExit("area.village", "area.forest");

            yield return MoveNear(controller, exit.Position);
            Assert.That(controller.Interact(), Is.True);
            yield return null;

            Assert.That(recording.MusicCalls, Does.Contain("bgm.world.forest"));
            Assert.That(recording.SfxCalls, Does.Contain("sfx.ui.confirm"));
        }

        [UnityTest]
        public IEnumerator WorldController_ConstructedPresenters_PlayDialogueAndShopAudio()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            var recording = WrapPresentation();
            Object.FindAnyObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.OpenDialogue("npc.elder"), Is.True);
            Assert.That(recording.SfxCalls, Does.Contain("sfx.dialogue.page"));

            var dialogue = Object.FindAnyObjectByType<DialoguePanelView>();
            FindButton(dialogue, "Close").onClick.Invoke();
            Assert.That(recording.SfxCalls, Does.Contain("sfx.ui.cancel"));
            var cancelCount = recording.SfxCalls.Count(cueId => cueId == "sfx.ui.cancel");

            Assert.That(controller.OpenDialogue("npc.merchant"), Is.True);
            Assert.That(controller.ShopPresenter.IsOpen, Is.True);
            Assert.That(
                recording.SfxCalls.Count(cueId => cueId == "sfx.ui.cancel"),
                Is.EqualTo(cancelCount));

            var shop = Object.FindAnyObjectByType<ShopPanelView>();
            FindButton(shop, "Buy_0").onClick.Invoke();
            Assert.That(recording.SfxCalls, Does.Contain("sfx.shop.buy"));

            FindButton(shop, "SellTab").onClick.Invoke();
            FindButton(shop, "Sell_0").onClick.Invoke();
            Assert.That(recording.SfxCalls, Does.Contain("sfx.shop.sell"));
        }

        [UnityTest]
        public IEnumerator BattleScene_PlaysBattleHitDownAndVictoryAudio()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            var recording = WrapPresentation();
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(recording.MusicCalls, Does.Contain("bgm.battle"));

            foreach (var enemy in controller.EngineForTests.State.Units
                         .Where(unit => unit.Team == Team.Enemy)
                         .ToArray())
            {
                enemy.ApplyRawDamage(int.MaxValue);
            }

            ForcePresenterRefresh(controller);
            yield return null;

            Assert.That(recording.SfxCalls, Does.Contain("sfx.battle.hit"));
            Assert.That(recording.SfxCalls, Does.Contain("sfx.battle.down"));
            Assert.That(recording.MusicCalls, Does.Contain("bgm.victory"));

            var stopCount = recording.StopMusicCount;
            Object.FindAnyObjectByType<BattleHudView>().ContinueButton.onClick.Invoke();
            Assert.That(recording.StopMusicCount, Is.GreaterThan(stopCount));
        }

        [UnityTest]
        public IEnumerator BattleScene_NormalAttack_PlaysAttackAudio()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            var recording = WrapPresentation();
            yield return SceneManager.LoadSceneAsync("Battle");
            yield return null;

            var controller = Object.FindAnyObjectByType<BattleSceneController>();
            var grid = Object.FindAnyObjectByType<BattleGridView>();
            var state = controller.EngineForTests.State;
            var active = state.Units.Single(unit => unit.Id == controller.ActiveUnitId);
            var enemy = state.Units.First(unit => unit.Team == Team.Enemy);
            var approach = BattleMovement
                .FindReachableDestinations(state, active)
                .Keys
                .Where(position =>
                    Mathf.Abs(position.X - enemy.Position.X) +
                    Mathf.Abs(position.Y - enemy.Position.Y) == 1)
                .OrderBy(position => position.X)
                .ThenBy(position => position.Y)
                .ToArray();
            Assert.That(approach, Is.Not.Empty);

            grid.Tap(approach[0]);
            yield return null;
            grid.Tap(enemy.Position);
            yield return null;

            Assert.That(recording.SfxCalls, Does.Contain("sfx.battle.attack"));
        }

        [UnityTest]
        public IEnumerator WorldBeginEncounter_PlaysEncounterAudio()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            var recording = WrapPresentation();
            Object.FindAnyObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            Assert.That(
                controller.BeginEncounter(FindEncounter("encounter.forest.bandits")),
                Is.True);
            Assert.That(recording.SfxCalls, Does.Contain("sfx.world.encounter"));
        }

        [UnityTest]
        public IEnumerator WorldReward_PlaysRewardAudio()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null;
            var recording = WrapPresentation();
            Object.FindAnyObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene("World");
            yield return null;

            var controller = Object.FindAnyObjectByType<WorldExplorationController>();
            var reward = FindFirstInteractable("area.village", WorldInteractableKind.Chest);
            yield return MoveNear(controller, reward.Position);
            Assert.That(controller.Interact(), Is.True);
            Assert.That(recording.SfxCalls, Does.Contain("sfx.world.reward"));
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            for (var index = 0; index < 180 && SceneManager.GetActiveScene().name != sceneName; index++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }

        private static RecordingPresentationService WrapPresentation()
        {
            var recording = new RecordingPresentationService(
                GameBootstrapper.Context.Get<IPresentationService>());
            GameBootstrapper.Context.Register<IPresentationService>(recording);
            return recording;
        }

        private static void ForcePresenterRefresh(BattleSceneController controller)
        {
            var field = typeof(BattleSceneController).GetField(
                "presenter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var presenter = (BattleUiPresenter)field.GetValue(controller);
            Assert.That(presenter, Is.Not.Null);
            presenter.Execute(null);
        }

        private static IEnumerator MoveNear(WorldExplorationController controller, Vector2 target)
        {
            for (var index = 0; index < 240; index++)
            {
                var offset = target - controller.PlayerPosition;
                if (offset.magnitude <= 0.55f)
                    break;
                controller.Joystick.SetValue(offset.normalized);
                yield return null;
            }

            controller.Joystick.SetValue(Vector2.zero);
            yield return null;
            Assert.That(Vector2.Distance(controller.PlayerPosition, target), Is.LessThan(0.7f));
        }

        private static WorldInteractableDefinition FindAreaExit(string sourceAreaId, string targetAreaId)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            Assert.That(catalog, Is.Not.Null);
            return catalog.All
                .OfType<WorldAreaDefinition>()
                .Single(area => area.Id == sourceAreaId)
                .Interactables
                .Single(value =>
                    value.Kind == WorldInteractableKind.AreaExit &&
                    value.TargetId == targetAreaId);
        }

        private static WorldInteractableDefinition FindFirstInteractable(
            string areaId,
            WorldInteractableKind kind)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            Assert.That(catalog, Is.Not.Null);
            return catalog.All
                .OfType<WorldAreaDefinition>()
                .Single(area => area.Id == areaId)
                .Interactables
                .First(value => value.Kind == kind);
        }

        private static WorldEncounterDefinition FindEncounter(string encounterId)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            Assert.That(catalog, Is.Not.Null);
            return catalog.All
                .OfType<WorldEncounterDefinition>()
                .Single(value => value.EncounterId == encounterId);
        }

        private static Button FindButton(Component root, string name)
        {
            var button = root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == name);
            Assert.That(button, Is.Not.Null, "Missing button: " + name);
            return button;
        }

        private sealed class RecordingPresentationService : IPresentationService
        {
            private readonly IPresentationService inner;

            public RecordingPresentationService(IPresentationService inner)
            {
                this.inner = inner;
            }

            public bool IsAvailable => inner.IsAvailable;
            public List<string> MusicCalls { get; } = new();
            public List<string> SfxCalls { get; } = new();
            public int StopMusicCount { get; private set; }
            public VisualClip GetVisualClip(string clipId) => inner.GetVisualClip(clipId);
            public Sprite GetSprite(string spriteId) => inner.GetSprite(spriteId);
            public AudioCue GetAudioCue(string cueId) => inner.GetAudioCue(cueId);
            public string GetAreaMusicCueId(string areaId) => inner.GetAreaMusicCueId(areaId);
            public string GetCharacterVisualPrefix(string definitionId) =>
                inner.GetCharacterVisualPrefix(definitionId);
            public Sprite GetNpcPortrait(string npcId) => inner.GetNpcPortrait(npcId);
            public Sprite GetItemIcon(string itemDefinitionId, string slotId) =>
                inner.GetItemIcon(itemDefinitionId, slotId);
            public Sprite GetUiSprite(string partId) => inner.GetUiSprite(partId);
            public void PlayMusic(string cueId)
            {
                MusicCalls.Add(cueId);
                inner.PlayMusic(cueId);
            }

            public void PlaySfx(string cueId)
            {
                SfxCalls.Add(cueId);
                inner.PlaySfx(cueId);
            }

            public void StopMusic()
            {
                StopMusicCount++;
                inner.StopMusic();
            }
        }
    }
}
