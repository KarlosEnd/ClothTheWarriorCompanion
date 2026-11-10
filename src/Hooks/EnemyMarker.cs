namespace ClothTheWarriorCompanion.Hooks;

/// <summary>
/// Marca enemigos atacados por el Caballero para coordinar objetivos prioritarios.
/// </summary>
internal static class EnemyMarker {
	internal static void OnSlashHit(Collider2D hitCollider, GameObject _) {
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
}

