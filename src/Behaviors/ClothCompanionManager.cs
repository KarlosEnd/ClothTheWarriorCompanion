using ClothTheWarriorCompanion.Config;

using HeroCtrl = HeroController;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Gestiona la invocación, ciclo de vida, persistencia y transiciones de escena de Cloth.
/// </summary>
internal static class ClothCompanionManager {
    internal const int TerrainMask = 1 << 8;
    internal const string CompanionGameObjectName = "Cloth Fighter Companion";

    internal static GameObject? CompanionInstance { get; private set; }
    public static bool FollowPlayerJumping { get; set; } = true;

    private static int _transitionSequenceId = 0;

    private static ClothFollowBehavior? Behavior =>
        CompanionInstance != null ? CompanionInstance.GetComponent<ClothFollowBehavior>() : null;

    /// <summary>
    /// Cloth si está presente y puede ser objetivo de los enemigos (no está excavando ni derrotada); si no, null.
    /// </summary>
    internal static GameObject? TargetableCompanion =>
        CompanionInstance != null && CompanionInstance.activeInHierarchy && Behavior != null && Behavior.IsTargetable
            ? CompanionInstance
            : null;

    /// <summary>
    /// Si el collider golpeado por el clavo del Caballero es parte de Cloth, le pasa el golpe y devuelve true.
    /// </summary>
    internal static bool TryHandleKnightSlash(Collider2D hitCollider) {
        var companion = CompanionInstance;
        if (companion == null || !hitCollider.transform.IsChildOf(companion.transform))
            return false;

        Behavior?.ReceiveKnightHit();
        return true;
    }

    /// <summary>
    /// Golpe que llega al HealthManager de Cloth por el pipeline normal del juego (HitTaker). Devuelve true si es de Cloth.
    /// </summary>
    internal static bool TryHandleHit(HealthManager target, HitInstance hit) {
        var companion = CompanionInstance;
        if (companion == null || !target.transform.IsChildOf(companion.transform))
            return false;

        // Golpes de la propia Cloth: se ignoran
        if (hit.Source != null && hit.Source.transform.IsChildOf(companion.transform))
            return true;

        ClothTheWarriorCompanionMod.Instance?.LogDebug($"Cloth alcanzada por el pipeline de golpes (daño {hit.DamageDealt}, tipo {hit.AttackType}).");
        Behavior?.ReceiveKnightHit();
        return true;
    }

    #region Position Utilities

    /// <summary>
    /// Intenta detectar la superficie de suelo debajo del punto proporcionado.
    /// Inicia un poco por encima para evitar arrancar dentro de un collider de terreno.
    /// </summary>
    internal static bool TryFindGround(Vector2 fromPosition, out Vector2 groundPosition, float maxDistance = 60f) {
        RaycastHit2D hit = Physics2D.Raycast(fromPosition + (Vector2.up * 0.3f), Vector2.down, maxDistance, TerrainMask);
        if (hit.collider != null) {
            groundPosition = hit.point;
            return true;
        }

        groundPosition = fromPosition;
        return false;
    }

    /// <summary>
    /// Retorna las coordenadas de los pies del Caballero (base inferior de su collider).
    /// </summary>
    private static Vector2 GetKnightFeetPosition() {
        var hero = HeroCtrl.instance;
        if (hero == null)
            return Vector2.zero;

        var boxCollider = hero.GetComponent<BoxCollider2D>();
        if (boxCollider != null) {
            var bounds = boxCollider.bounds;
            return new Vector2(bounds.center.x, bounds.min.y);
        }

        return hero.transform.position;
    }

    /// <summary>
    /// Obtiene el punto de suelo correspondiente a la posición del Caballero.
    /// Si el héroe está en el aire, proyecta hacia el suelo inferior garantizando aterrizajes válidos.
    /// </summary>
    internal static Vector2 GetKnightGroundPosition() {
        Vector2 feetPosition = GetKnightFeetPosition();
        if (HeroCtrl.instance != null && HeroCtrl.instance.cState.onGround) {
            return feetPosition;
        }
        return TryFindGround(feetPosition, out Vector2 groundPoint) ? groundPoint : feetPosition;
    }

    #endregion

    #region Lifecycle & Control

    /// <summary>
    /// Conmuta la presencia de Cloth (invocar o retirar).
    /// </summary>
    internal static void ToggleSummon() {
        if (CompanionInstance == null) {
            Summon();
        } else if (!CompanionInstance.activeSelf) {
            Despawn();
            Summon();
        } else {
            Despawn();
        }
    }

    /// <summary>
    /// Reubica inmediatamente a Cloth en la posición del Caballero.
    /// </summary>
    internal static void RecallToKnight() {
        if (CompanionInstance is null or { activeSelf: false })
            return;
        Behavior?.AppearAt(GetKnightGroundPosition());
    }

    /// <summary>
    /// Ordena a Cloth realizar una excavación hacia la posición del Caballero.
    /// </summary>
    internal static void BurrowToKnight() {
        if (CompanionInstance != null && CompanionInstance.activeSelf) {
            Behavior?.ForceBurrow(GetKnightGroundPosition());
        }
    }

    private static void Summon() {
        if (ClothTheWarriorCompanionMod.ClothFighterPrefab == null) {
            throw new InvalidOperationException("Prefab de Cloth no está cargado.");
        }

        var hero = HeroCtrl.instance;
        if (hero == null)
            return;

        GameObject companion = UObject.Instantiate(
            ClothTheWarriorCompanionMod.ClothFighterPrefab,
            hero.transform.position,
            Quaternion.identity
        ) ?? throw new NullReferenceException("Failed to instantiate Cloth companion");

        companion.name = CompanionGameObjectName;

        // Los golpes del Caballero solo se registran en las capas Enemies / Attackable
        int enemiesLayer = LayerMask.NameToLayer("Enemies");
        int attackableLayer = LayerMask.NameToLayer("Attackable");
        ClothTheWarriorCompanionMod.Instance?.LogDebug($"Capa de Cloth: {LayerMask.LayerToName(companion.layer)} ({companion.layer}).");
        if (enemiesLayer >= 0 && companion.layer != enemiesLayer && companion.layer != attackableLayer) {
            companion.layer = enemiesLayer;
            ClothTheWarriorCompanionMod.Instance?.LogDebug("Capa de Cloth cambiada a Enemies.");
        }
        UObject.DontDestroyOnLoad(companion);

        // Desactivar FSMs originales de IA enemiga/NPC antes de activar
        foreach (var fsm in companion.GetComponentsInChildren<PlayMakerFSM>(true)) {
            fsm.enabled = false;
        }

        // Remover daño hacia el héroe
        foreach (var damageHero in companion.GetComponentsInChildren<DamageHero>(true)) {
            UObject.Destroy(damageHero);
        }

        // Deshabilitar HealthManager para no ser catalogada como enemigo
        foreach (var healthManager in companion.GetComponentsInChildren<HealthManager>(true)) {
            healthManager.enabled = false;
        }

        companion.AddComponent<ClothFollowBehavior>();
        companion.AddComponent<ClothHitResponder>();
        companion.AddComponent<ClothAudioController>();

        //DEBUGGER:
        companion.AddComponent<ClothTheWarriorCompanion.DebugTools.ClothVisualDebugger>();

        CompanionInstance = companion;

        GameManager.instance?.StartCoroutine(ActivateCompanionRoutine(companion));
    }

    private static void Despawn() {
        Hooks.EnemyTargetRedirector.RestoreAll();
        if (CompanionInstance != null) {
            UObject.Destroy(CompanionInstance);
            CompanionInstance = null;
        }
    }

    private static IEnumerator ActivateCompanionRoutine(GameObject companion) {
        yield return null;
        if (companion == null)
            yield break;

        companion.SetActive(true);
        companion.GetComponent<ClothFollowBehavior>()?.AppearAt(GetKnightGroundPosition());
    }

    internal static IEnumerator SceneTransitionSpawnRoutine() {
        int sequenceId = ++_transitionSequenceId;
        GameObject? companion = CompanionInstance;
        if (companion == null)
            yield break;

        Hooks.EnemyTargetRedirector.RestoreAll();
        companion.SetActive(false);
        yield return null;

        var gameManager = GameManager.instance;
        if (gameManager == null)
            yield break;

        // Esperar a que la nueva escena finalice carga y esté lista
        float elapsedWait = 0f;
        while (elapsedWait < 15f && (gameManager.gameState != GlobalEnums.GameState.PLAYING || HeroCtrl.instance == null)) {
            elapsedWait += Time.unscaledDeltaTime;
            yield return null;
            if (sequenceId != _transitionSequenceId || companion == null)
                yield break;
        }

        if (companion == null || sequenceId != _transitionSequenceId)
            yield break;

        // Si regresamos al menú principal o cinemática no interactiva, retirar compañera
        if (!gameManager.IsGameplayScene() || HeroCtrl.instance == null) {
            Despawn();
            yield break;
        }

        // Breve margen para que el héroe termine de asentarse en la superficie tras la entrada
        yield return new WaitForSeconds(0.75f);
        if (companion == null || sequenceId != _transitionSequenceId)
            yield break;

        companion.SetActive(true);
        companion.GetComponent<ClothFollowBehavior>()?.AppearAt(GetKnightGroundPosition());
    }

    #endregion
}

