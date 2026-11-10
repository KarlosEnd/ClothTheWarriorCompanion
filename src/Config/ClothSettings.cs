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
    /// Distancia al enemigo a la que se inicia la animación del golpe con el garrote en el aire.
    /// </summary>
    public float AttackAnimDistance { get; set; } = 6f;

    /// <summary>
    /// Distancia al enemigo requerida para conectar el golpe aéreo antes de impactar el suelo.
    /// </summary>
    public float AirHitDistance { get; set; } = 2.5f;

    /// <summary>
    /// Cantidad de daño infligido por el impacto en área (slam) del garrote.
    /// </summary>
    public int SlamDamageAmount { get; set; } = 40;

    /// <summary>
    /// Radio del área de impacto (slam) del garrote.
    /// </summary>
    public float SlamRadius { get; set; } = 5f;

    /// <summary>
    /// Tiempo de espera (cooldown) tras finalizar un ataque antes de poder ejecutar otro.
    /// </summary>
    public float AttackCooldown { get; set; } = 1.0f;

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
        if (string.IsNullOrWhiteSpace(value)) return false;
        try {
            _ = Input.GetKey(value);
            return true;
        } catch (ArgumentException) {
            return false;
        }
    }

    #endregion
}

