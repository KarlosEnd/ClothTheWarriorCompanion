using ClothTheWarriorCompanion.Behaviors;
using ClothTheWarriorCompanion.Config;
using ClothTheWarriorCompanion.Fsm;
using ClothTheWarriorCompanion.Hooks;
using USceneManager = UnityEngine.SceneManagement.SceneManager;
using UScene = UnityEngine.SceneManagement.Scene;

namespace ClothTheWarriorCompanion;

/// <summary>
/// Clase principal del mod "Cloth The Warrior Companion" para Hollow Knight.
/// </summary>
public sealed class ClothTheWarriorCompanionMod : Mod, ITogglableMod {
	private const string ClothSceneName = "Fungus3_23_boss";
	private const string ClothScenePath = "Battle Scene/Cloth Entry/Cloth Fighter";

	public static ClothTheWarriorCompanionMod? Instance { get; private set; }

	internal static GameObject? ClothFighterPrefab { get; private set; }

	public override string GetVersion() => Assembly
		.GetExecutingAssembly()
		.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
		.InformationalVersion ?? "1.0.0";

	public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloads) {
		if (Instance != null) return;
		Instance = this;

		ClothSettings.Load();
		ClothQuickMenu.Initialize();

		if (preloads != null && preloads.TryGetValue(ClothSceneName, out var sceneObjects)) {
			if (sceneObjects.TryGetValue(ClothScenePath, out var prefab)) {
				ClothFighterPrefab = prefab;
				LogDebug("Cloth fighter prefab preloaded successfully.");
			}
		}

		ModHooks.HeroUpdateHook += CompanionInputListener.OnHeroUpdate;
		ModHooks.SlashHitHook += EnemyMarker.OnSlashHit;
		On.PlayMakerFSM.OnEnable += ClothFsmModifier.OnFsmEnableHook;

		USceneManager.activeSceneChanged += OnActiveSceneChanged;
	}

	public void Unload() {
		ModHooks.HeroUpdateHook -= CompanionInputListener.OnHeroUpdate;
		ModHooks.SlashHitHook -= EnemyMarker.OnSlashHit;
		On.PlayMakerFSM.OnEnable -= ClothFsmModifier.OnFsmEnableHook;
		USceneManager.activeSceneChanged -= OnActiveSceneChanged;

		Instance = null;
	}

	private void OnActiveSceneChanged(UScene previousScene, UScene nextScene) {
		if (ClothCompanionManager.CompanionInstance != null) {
			GameManager.instance?.StartCoroutine(ClothCompanionManager.SceneTransitionSpawnRoutine());
		}
	}

	public override List<(string, string)> GetPreloadNames() => new() {
		(ClothSceneName, ClothScenePath)
	};
}
