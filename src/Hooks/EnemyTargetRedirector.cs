using ClothTheWarriorCompanion.Behaviors;
using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Hooks;

/// <summary>
/// Permite que los enemigos elijan a Cloth como objetivo. Las FSM de los enemigos guardan al Caballero en variables
/// GameObject ("Hero", "Target"...). Periódicamente, si Cloth está más cerca de un enemigo que el Caballero,
/// esas variables se apuntan a Cloth; al dejar de cumplirse (o si Cloth desaparece) se devuelven al Caballero.
/// Los enemigos que localizan al Caballero por código propio, tags o rangos no se ven afectados.
/// </summary>
internal static class EnemyTargetRedirector {
	private static readonly List<FsmGameObject> Swapped = new();

	/// <summary>
	/// Reasigna objetivos. Se llama varias veces por segundo mientras Cloth está en escena.
	/// </summary>
	internal static void Retarget(GameObject cloth, bool clothIsTargetable) {
		RestoreAll();

		var settings = ClothSettings.Current;
		var hero = HeroController.instance;
		if (hero == null || !clothIsTargetable || !settings.EnemiesTargetCloth)
			return;

		GameObject heroObject = hero.gameObject;
		Vector2 clothPosition = cloth.transform.position;
		Vector2 heroPosition = heroObject.transform.position;

		foreach (var healthManager in UObject.FindObjectsOfType<HealthManager>()) {
			GameObject enemy = healthManager.gameObject;
			if (enemy == cloth || !healthManager.isActiveAndEnabled || healthManager.isDead)
				continue;

			Vector2 enemyPosition = enemy.transform.position;
			float distanceToCloth = Vector2.Distance(enemyPosition, clothPosition);
			float distanceToHero = Vector2.Distance(enemyPosition, heroPosition);
			if (distanceToCloth > settings.EnemyTargetRange || distanceToCloth >= distanceToHero)
				continue;

			foreach (var fsm in enemy.GetComponents<PlayMakerFSM>()) {
				var variables = fsm.FsmVariables?.GameObjectVariables;
				if (variables == null) continue;

				foreach (var variable in variables) {
					if (variable.Value == heroObject) {
						variable.Value = cloth;
						Swapped.Add(variable);
					}
				}
			}
		}
	}

	/// <summary>
	/// Devuelve al Caballero todas las variables que se habían apuntado a Cloth.
	/// </summary>
	internal static void RestoreAll() {
		if (Swapped.Count == 0)
			return;

		var hero = HeroController.instance;
		GameObject? heroObject = hero != null ? hero.gameObject : null;
		foreach (var variable in Swapped) {
			if (variable != null)
				variable.Value = heroObject;
		}
		Swapped.Clear();
	}
}
