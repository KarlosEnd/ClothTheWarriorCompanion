using System.IO;

using Newtonsoft.Json;

namespace ClothTheWarriorCompanion.Config;

/// <summary>
/// Configuración completa de controles, comportamiento, físicas y combate para Cloth.
/// Se guarda directamente en la carpeta del mod (ClothSettings.json) y puede modificarse
/// tanto editando el archivo como desde el menú de opciones del juego.
/// </summary>
public sealed class ClothSettings {
    private static readonly string ConfigFilePath = Path.Combine(
        Path.GetDirectoryName(typeof(ClothSettings).Assembly.Location) ?? ".",
        "ClothSettings.json"
    );

    /// <summary>
    /// Instancia activa de la configuración en tiempo de ejecución.
    /// </summary>
    public static ClothSettings Current { get; private set; } = new();

    #region Keybindings

    [JsonIgnore]
    private string _summonKey = "y";

    [JsonIgnore]
    private string _recallKey = "u";

    [JsonIgnore]
    private string _burrowKey = "i";

    [JsonIgnore]
    private string _quickMenuKey = "o";

    /// <summary>
    /// Tecla para invocar o desinvocar a Cloth.
    /// </summary>
    public string SummonKey {
        get => _summonKey;
        set {
            if (IsValidKey(value)) {
                _summonKey = value;
            }
        }
    }

    /// <summary>
    /// Tecla para reubicar inmediatamente a Cloth junto al Caballero.
    /// </summary>
    public string RecallKey {
        get => _recallKey;
        set {
            if (IsValidKey(value)) {
                _recallKey = value;
            }
        }
    }

    /// <summary>
    /// Tecla para ordenar a Cloth que excave y reaparezca cerca del Caballero.
    /// </summary>
    public string BurrowKey {
        get => _burrowKey;
        set {
            if (IsValidKey(value)) {
                _burrowKey = value;
            }
        }
    }

    /// <summary>
    /// Tecla para abrir rápidamente el menú de configuración de Cloth en la partida.
    /// </summary>
    public string QuickMenuKey {
        get => _quickMenuKey;
        set {
            if (IsValidKey(value)) {
                _quickMenuKey = value;
            }
        }
    }

    #endregion

    #region Follow & Navigation

    /// <summary>
    /// Distancia horizontal mínima respecto al Caballero para que Cloth empiece a seguirle.
    /// </summary>
    public float FollowDistanceX { get; set; } = 7f;

    /// <summary>
    /// Distancia vertical mínima respecto al Caballero para que Cloth empiece a seguirle.
    /// </summary>
    public float FollowDistanceY { get; set; } = 3f;

    /// <summary>
    /// Distancia horizontal máxima para realizar un salto de seguimiento; si es mayor, excava (burrow).
    /// </summary>
    public float BurrowThresholdX { get; set; } = 28f;

    /// <summary>
    /// Distancia horizontal máxima para saltar hacia un enemigo; si es mayor, excava hacia él.
    /// </summary>
    public float BurrowThresholdEnemyX { get; set; } = 30f;

    /// <summary>
    /// Desnivel vertical negativo máximo (caída) permitido antes de forzar excavación.
    /// </summary>
    public float MaxFallDropDeltaY { get; set; } = -14f;

    /// <summary>
    /// Tiempo de espera (cooldown) tras completar un salto de seguimiento antes de volver a moverse.
    /// </summary>
    public float FollowCooldownAfterJump { get; set; } = 0.5f;

    /// <summary>
    /// Tiempo de espera tras emerger de una excavación antes de volver a moverse.
    /// </summary>
    public float FollowCooldownAfterBurrow { get; set; } = 1.0f;

    #endregion

    #region Jump & Physics

    /// <summary>
    /// Velocidad horizontal máxima alcanzada durante un salto normal de navegación.
    /// </summary>
    public float JumpNormalMaxSpeedX { get; set; } = 20f;

    /// <summary>
    /// Altura mínima de salto en unidades de mundo.
    /// </summary>
    public float MinJumpHeight { get; set; } = 3f;

    /// <summary>
    /// Altura máxima de salto en unidades de mundo.
    /// </summary>
    public float MaxJumpHeight { get; set; } = 9f;

    /// <summary>
    /// Altura adicional sobre el desnivel objetivo para calcular el arco parabólico.
    /// </summary>
    public float JumpHeightBuffer { get; set; } = 2f;

    /// <summary>
    /// Tiempo mínimo (segundos) para alcanzar el punto más alto del salto (ápice).
    /// </summary>
    public float MinApexTime { get; set; } = 0.4f;

    /// <summary>
    /// Tiempo máximo (segundos) para alcanzar el ápice del salto.
    /// </summary>
    public float MaxApexTime { get; set; } = 0.75f;

    /// <summary>
    /// Escala de gravedad aplicada a Cloth cuando está en reposo sobre el suelo.
    /// </summary>
    public float RestGravity { get; set; } = 3f;

    /// <summary>
    /// Margen de seguridad con respecto al techo para evitar colisiones bruscas al saltar.
    /// </summary>
    public float CeilingSafetyMargin { get; set; } = 0.3f;

    /// <summary>
    /// Alcance vertical máximo que Cloth considera alcanzable de un solo salto.
    /// </summary>
    public float MaxVerticalReach { get; set; } = 7f;

    /// <summary>
    /// Distancia máxima al suelo bajo el Caballero para considerarlo "seguro" (permite rescates).
    /// </summary>
    public float maxAllowedGroundDistance { get; set; } = 5f;

    #endregion

    #region Combat & Detection

    /// <summary>
    /// Radio máximo de detección para que Cloth localice enemigos cercanos.
    /// </summary>
    public float DetectionRadiusCloth { get; set; } = 22f;

    /// <summary>
    /// Radio máximo respecto al Caballero para que un enemigo sea considerado objetivo válido de protección.
    /// </summary>
    public float ProtectionRadiusHero { get; set; } = 25f;

    /// <summary>
    /// Intervalo en segundos entre cada escaneo de enemigos en la escena.
    /// </summary>
    public float EnemyScanInterval { get; set; } = 0.4f;

    /// <summary>
    /// Distancia máxima a la que Cloth inicia su salto de ataque hacia el enemigo.
    /// </summary>
    public float AttackRange { get; set; } = 18f;

    /// <summary>
    /// Velocidad horizontal máxima del salto de embestida de ataque.
    /// </summary>
    public float AttackMaxSpeedX { get; set; } = 24f;

    /// <summary>
    /// Multiplicador de gravedad en la fase descendente del ataque (caída en picado).
    /// </summary>
    public float DiveGravityMultiplier { get; set; } = 2.5f;

    /// <summary>
    /// Distancia (hitbox -> enemigo) a la que se inicia la animación del golpe con el garrote en el aire.
    /// </summary>
    public float AttackAnimDistance { get; set; } = 6f;

    /// <summary>
    /// Distancia (hitbox -> enemigo) requerida para conectar el golpe aéreo antes de impactar el suelo.
    /// </summary>
    public float AirHitDistance { get; set; } = 2.5f;

    /// <summary>
    /// Cantidad de daño infligido por el impacto del garrote.
    /// </summary>
    public int SlamDamageAmount { get; set; } = 40;

    /// <summary>
    /// Tiempo de espera (cooldown) tras finalizar un ataque antes de poder ejecutar otro.
    /// </summary>
    public float AttackCooldown { get; set; } = 1.0f;

    #endregion

    #region Attack Hitbox

    /// <summary>
    /// Desplazamiento horizontal del centro de la hitbox de ataque respecto al transform de Cloth.
    /// Positivo = hacia adelante (se invierte automáticamente con la orientación).
    /// </summary>
    public float AttackBoxOffsetX { get; set; } = 2f;

    /// <summary>
    /// Desplazamiento vertical del centro de la hitbox de ataque respecto al transform de Cloth.
    /// </summary>
    public float AttackBoxOffsetY { get; set; } = -0.5f;

    /// <summary>
    /// Ancho de la hitbox de ataque.
    /// </summary>
    public float AttackBoxWidth { get; set; } = 6f;

    /// <summary>
    /// Alto de la hitbox de ataque.
    /// </summary>
    public float AttackBoxHeight { get; set; } = 5f;

    #endregion

    #region Close Attack

    /// <summary>
    /// Distancia (hitbox -> enemigo) por debajo de la cual se usa el salto corto. 0 = desactivado.
    /// </summary>
    public float CloseAttackDistance { get; set; } = 7f;

    /// <summary>
    /// Desnivel vertical máximo (en valor absoluto) para poder usar el salto corto.
    /// </summary>
    public float CloseAttackMaxDeltaY { get; set; } = 3f;

    /// <summary>
    /// Altura del hop del ataque corto.
    /// </summary>
    public float CloseJumpHeight { get; set; } = 1.2f;

    /// <summary>
    /// Tiempo hasta el ápice del hop del ataque corto.
    /// </summary>
    public float CloseApexTime { get; set; } = 0.18f;

    /// <summary>
    /// Velocidad horizontal máxima del ataque corto.
    /// </summary>
    public float CloseAttackSpeedX { get; set; } = 14f;

    /// <summary>
    /// Multiplicador de gravedad al descender en el ataque corto.
    /// </summary>
    public float CloseDiveGravityMultiplier { get; set; } = 2.5f;

    /// <summary>
    /// Duración (s) de la animación "Jump Attack Antic" antes de despegar en el ataque corto.
    /// </summary>
    public float CloseAnticDuration { get; set; } = 0.1f;

    /// <summary>
    /// Segundos antes del final estimado del vuelo en que se reproduce "Jump Attack" (solo al descender).
    /// </summary>
    public float CloseAnimLeadTime { get; set; } = 0.12f;

    /// <summary>
    /// Distancia (hitbox -> enemigo) a la que se reproduce "Jump Attack" mientras desciende.
    /// </summary>
    public float CloseAnimDistance { get; set; } = 4f;

    /// <summary>
    /// Distancia (hitbox -> enemigo) para conectar el golpe en el aire durante el ataque corto.
    /// </summary>
    public float CloseAirHitDistance { get; set; } = 2.5f;

    /// <summary>
    /// Tiempo mínimo (s) tras despegar antes de considerar que Cloth aterrizó (ataque corto).
    /// </summary>
    public float CloseLandCheckDelay { get; set; } = 0.1f;

    /// <summary>
    /// Escala de la duración de "Jump Attack Recover" tras el ataque corto (1 = completa, 0 = omitir).
    /// </summary>
    public float CloseRecoverDurationScale { get; set; } = 1f;

    /// <summary>
    /// Cooldown tras finalizar un ataque corto.
    /// </summary>
    public float CloseAttackCooldown { get; set; } = 0.6f;

    #endregion

    #region Attack Animation & Timing

    /// <summary>
    /// Duración (s) de "Jump Attack Antic" antes de despegar en el ataque normal.
    /// </summary>
    public float AttackAnticDuration { get; set; } = 0.1f;

    /// <summary>
    /// Segundos antes del final estimado del vuelo en que se reproduce "Jump Attack" (solo al descender).
    /// </summary>
    public float AttackAnimLeadTime { get; set; } = 0.25f;

    /// <summary>
    /// Tiempo mínimo (s) tras despegar antes de considerar que Cloth aterrizó (ataque normal).
    /// </summary>
    public float AttackLandCheckDelay { get; set; } = 0.25f;

    /// <summary>
    /// Escala de la duración de "Jump Attack Recover" tras el ataque normal (1 = completa, 0 = omitir).
    /// </summary>
    public float RecoverDurationScale { get; set; } = 1f;

    #endregion

    #region Normal Jump Extras

    /// <summary>
    /// Multiplicador de gravedad en la fase descendente del salto normal de seguimiento (mínimo 1).
    /// </summary>
    public float JumpFallGravityMultiplier { get; set; } = 1f;

    /// <summary>
    /// Escala de la duración de "Jump Attack Recover" al aterrizar del salto normal (1 = completa, 0 = omitir).
    /// </summary>
    public float JumpRecoverDurationScale { get; set; } = 1f;

    #endregion

    #region Post-Attack Fall

    /// <summary>
    /// Segundos tras el golpe (haya conectado o no) antes de volver a la animación "Jump" si Cloth sigue en el aire.
    /// Aplica a los dos ataques (normal y corto).
    /// </summary>
    public float PostAttackFallDelay { get; set; } = 0.1f;

    /// <summary>
    /// Cantidad de frames de "Jump" que se reproducen tras el golpe antes de pasar a "Jump Attack Antic" como caída.
    /// </summary>
    public int PostAttackJumpFrames { get; set; } = 4;

    /// <summary>
    /// Frame (empezando en 1) desde el cual se reproduce "Jump Attack Recover" al aterrizar tras esa caída.
    /// </summary>
    public int PostAttackFallRecoverStartFrame { get; set; } = 2;

    #endregion

    #region Retreat Hop

    /// <summary>
    /// Distancia horizontal (cuerpo de Cloth -> borde del enemigo) por debajo de la cual retrocede en lugar de golpear. 0 = desactivado.
    /// </summary>
    public float RetreatTriggerDistance { get; set; } = 2.5f;

    /// <summary>
    /// Desnivel vertical máximo (cuerpo de Cloth -> borde del enemigo) para considerar que el enemigo está "pegado".
    /// </summary>
    public float RetreatMaxDeltaY { get; set; } = 3f;

    /// <summary>
    /// Distancia horizontal que retrocede el salto.
    /// </summary>
    public float RetreatDistance { get; set; } = 6f;

    /// <summary>
    /// Altura del salto de retroceso.
    /// </summary>
    public float RetreatJumpHeight { get; set; } = 1.5f;

    /// <summary>
    /// Tiempo hasta el ápice del salto de retroceso.
    /// </summary>
    public float RetreatApexTime { get; set; } = 0.22f;

    /// <summary>
    /// Velocidad horizontal máxima del salto de retroceso.
    /// </summary>
    public float RetreatSpeedX { get; set; } = 14f;

    /// <summary>
    /// Multiplicador de gravedad al descender en el salto de retroceso.
    /// </summary>
    public float RetreatDiveGravityMultiplier { get; set; } = 2.5f;

    /// <summary>
    /// Duración (s) de "Jump Attack Antic" antes de despegar en el retroceso.
    /// </summary>
    public float RetreatAnticDuration { get; set; } = 0.1f;

    /// <summary>
    /// Tiempo mínimo (s) tras despegar antes de considerar que Cloth aterrizó (retroceso).
    /// </summary>
    public float RetreatLandCheckDelay { get; set; } = 0.1f;

    /// <summary>
    /// Escala de la duración de "Jump Attack Recover" tras el retroceso (1 = completa, 0 = omitir).
    /// </summary>
    public float RetreatRecoverDurationScale { get; set; } = 1f;

    /// <summary>
    /// Tiempo mínimo (s) entre un retroceso y el siguiente (evita retrocesos en bucle).
    /// </summary>
    public float RetreatCooldown { get; set; } = 1.5f;

    #endregion

    #region Damage & Targeting

    /// <summary>
    /// Si está activo, Cloth recibe daño y knockback de los ataques y el contacto de los enemigos.
    /// </summary>
    public bool CanTakeDamage { get; set; } = true;

    /// <summary>
    /// Vida máxima de Cloth, en la misma unidad que el daño de los enemigos al Caballero (máscaras). 0 = sin límite.
    /// </summary>
    public int MaxHealth { get; set; } = 10;

    /// <summary>
    /// Segundos de invulnerabilidad tras recibir un golpe.
    /// </summary>
    public float HurtInvulnerability { get; set; } = 0.8f;

    /// <summary>
    /// Duración (s) del aturdimiento y del impulso de knockback tras recibir un golpe.
    /// </summary>
    public float HurtStunDuration { get; set; } = 0.35f;

    /// <summary>
    /// Velocidad horizontal inicial del knockback (se aleja del atacante).
    /// </summary>
    public float KnockbackSpeedX { get; set; } = 12f;

    /// <summary>
    /// Velocidad vertical inicial del knockback.
    /// </summary>
    public float KnockbackSpeedY { get; set; } = 8f;

    /// <summary>
    /// FPS con los que se reproduce "Jump Attack Antic" (3 frames) como animación de daño. 0 = animación desactivada.
    /// </summary>
    public float HurtAnimFps { get; set; } = 12f;

    /// <summary>
    /// Segundos que Cloth permanece fuera de combate tras quedarse sin vida, antes de reaparecer junto al Caballero.
    /// </summary>
    public float DefeatedReturnDelay { get; set; } = 5f;

    /// <summary>
    /// Si está activo, los enemigos pueden elegir a Cloth como objetivo (cuando está más cerca de ellos que el Caballero).
    /// </summary>
    public bool EnemiesTargetCloth { get; set; } = true;

    /// <summary>
    /// Distancia máxima enemigo -> Cloth para que el enemigo pueda elegirla como objetivo.
    /// </summary>
    public float EnemyTargetRange { get; set; } = 20f;

    #endregion

    #region Test Options

    /// <summary>
    /// Si está activo, Cloth considera al Caballero como objetivo de ataque (solo pruebas, no le hace daño).
    /// Base para un futuro modo duelo.
    /// </summary>
    public bool AttackPlayer { get; set; } = false;

    #endregion

    #region Safety & Rescue

    /// <summary>
    /// Distancia total máxima respecto al Caballero antes de forzar un rescate / teletransporte inmediato.
    /// </summary>
    public float RescueDistanceTotal { get; set; } = 45f;

    /// <summary>
    /// Desnivel vertical máximo respecto al Caballero antes de forzar rescate.
    /// </summary>
    public float RescueDistanceY { get; set; } = 30f;

    /// <summary>
    /// Caída máxima en el eje Y por debajo del Caballero (abismo) antes de forzar rescate.
    /// </summary>
    public float RescueAbyssY { get; set; } = 40f;

    /// <summary>
    /// Tiempo límite en segundos en estado ocupado (busy) antes de forzar un rescate por atascamiento.
    /// </summary>
    public float BusyRescueTimeout { get; set; } = 8.0f;

    #endregion

    #region Persistence (Load & Save)

    /// <summary>
    /// Carga la configuración desde el archivo ClothSettings.json en la carpeta del mod.
    /// Si no existe, genera el archivo con los valores predeterminados.
    /// </summary>
    public static void Load() {
        try {
            if (File.Exists(ConfigFilePath)) {
                string json = File.ReadAllText(ConfigFilePath);
                var loaded = JsonConvert.DeserializeObject<ClothSettings>(json);
                if (loaded != null) {
                    Current = loaded;
                    ClothTheWarriorCompanionMod.Instance?.Log($"Configuración cargada desde {ConfigFilePath}");
                    return;
                }
            }
        } catch (Exception ex) {
            ClothTheWarriorCompanionMod.Instance?.LogError($"Error al cargar configuración ({ConfigFilePath}): {ex.Message}");
        }

        // Si no existe o falló, guardar los valores iniciales
        Save();
    }

    /// <summary>
    /// Guarda la configuración actual en el archivo ClothSettings.json en la carpeta del mod.
    /// </summary>
    public static void Save() {
        try {
            string? dir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
            ClothTheWarriorCompanionMod.Instance?.LogDebug($"Configuración guardada en {ConfigFilePath}");
        } catch (Exception ex) {
            ClothTheWarriorCompanionMod.Instance?.LogError($"Error al guardar configuración ({ConfigFilePath}): {ex.Message}");
        }
    }

    private static bool IsValidKey(string value) {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        try {
            _ = Input.GetKey(value);
            return true;
        } catch (ArgumentException) {
            return false;
        }
    }

    #endregion
}
