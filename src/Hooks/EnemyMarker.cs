using ClothTheWarriorCompanion.Behaviors;

namespace ClothTheWarriorCompanion.Hooks;

/// <summary>
/// Marca enemigos atacados por el Caballero para coordinar objetivos prioritarios.
/// </summary>
internal static class EnemyMarker {
	internal static void OnSlashHit(Collider2D hitCollider, GameObject slashObject) {
		// Si el golpe de clavo alcanzó a Cloth, lo gestiona ella (daño y knockback); no es un enemigo
		if (hitCollider != null && ClothCompanionManager.TryHandleKnightSlash(hitCollider))
			return;

		if (hitCollider != null) {
			MarkTarget(hitCollider.gameObject);
		}
	}

	private static void MarkTarget(GameObject targetGameObject) {
		if (targetGameObject.TryGetComponent<HealthManager>(out var healthManager)) {
			if (healthManager.isActiveAndEnabled && !healthManager.isDead && !healthManager.IsInvincible && healthManager.hp > 0) {
				targetGameObject.tag = "Boss";
				ClothTheWarriorCompanionMod.Instance?.LogDebug($"Marked '{targetGameObject.name}' as prioritized target");
			}
		}
	}

	/// <summary>
	/// Todos los golpes del Caballero (clavo, hechizos...) pasan por HealthManager.Hit mediante HitTaker.
	/// Si el objetivo es Cloth, el golpe se redirige a su propio sistema de daño y no llega al HealthManager
	/// (que está deshabilitado y podría "matarla" o disparar efectos de enemigo).
	/// </summary>
	internal static void OnHealthManagerHit(On.HealthManager.orig_Hit orig, HealthManager self, HitInstance hitInstance) {
		if (self != null && ClothCompanionManager.TryHandleHit(self, hitInstance))
			return;

		orig(self, hitInstance);
	}
}
