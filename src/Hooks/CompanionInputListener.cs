using ClothTheWarriorCompanion.Behaviors;

namespace ClothTheWarriorCompanion.Hooks;

/// <summary>
/// Escucha las entradas del jugador durante el ciclo de actualización del HeroController.
/// </summary>
internal static class CompanionInputListener {
	internal static void OnHeroUpdate() {
		var modInstance = ClothTheWarriorCompanionMod.Instance;
		if (modInstance == null) return;

		var settings = modInstance.GlobalSettings;

		if (Input.GetKeyDown(settings.SummonKey)) {
			modInstance.LogFine("Summon key triggered");
			ClothCompanionManager.ToggleSummon();
		} else if (Input.GetKeyDown(settings.RecallKey)) {
			modInstance.LogFine("Recall key triggered");
			ClothCompanionManager.RecallToKnight();
		} else if (Input.GetKeyDown(settings.BurrowKey)) {
			modInstance.LogFine("Burrow key triggered");
			ClothCompanionManager.BurrowToKnight();
		}
	}
}

