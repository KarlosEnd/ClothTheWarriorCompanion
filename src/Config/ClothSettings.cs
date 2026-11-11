using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

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

    /// <summary>
    /// Si está activo, Cloth predecirá la posición futura del enemigo basándose en su velocidad para ajustar sus saltos.
    /// </summary>
    public bool PredictTargetMovement { get; set; } = true;

    /// <summary>
    /// Tiempo máximo (en segundos) a predecir hacia el futuro.
    /// </summary>
    public float PredictionMaxTime { get; set; } = 0.8f;

    /// <summary>
    /// Distancia máxima de ajuste permitido por la predicción. Evita que calcule puntos demasiado lejanos si el enemigo va muy rápido.
    /// </summary>
    public float PredictionMaxDistance { get; set; } = 15f;

    /// <summary>
    /// Margen de seguridad entre el collider de Cloth y el del enemigo para que la hitbox conecte sin que Cloth choque su cuerpo primero.
    /// </summary>
    public float HitboxClearanceMargin { get; set; } = 0.35f;

    /// <summary>
    /// Anticipa si un enemigo va a colisionar con Cloth por trayectoria calculando el Tiempo hasta Colisión (TTC).
    /// </summary>
    public bool DefensivePredictionEnabled { get; set; } = true;

    /// <summary>
    /// Tiempo máximo hacia el futuro para detectar amenazas de colisión inminente (en segundos).
    /// </summary>
    public float ThreatLookaheadTime { get; set; } = 1.2f;

    /// <summary>
    /// Margen extra de seguridad alrededor de los colliders para la detección de colisiones inminentes.
    /// </summary>
    public float ThreatMargin { get; set; } = 0.5f;

    /// <summary>
    /// Margen mínimo para que el aterrizaje de esquiva quede fuera de la línea de fuego del enemigo.
    /// </summary>
    public float DodgeClearanceMargin { get; set; } = 1.5f;

    /// <summary>
    /// Distancia mínima de desplazamiento al realizar una esquiva evasiva.
    /// </summary>
    public float DodgeMinDistance { get; set; } = 6.0f;

    /// <summary>
    /// Si una amenaza colisionará pronto y no alcanza a atacar a tiempo, realiza un salto corto de esquiva y contraataque inmediato.
    /// </summary>
    public bool ThreatDodgeHopEnabled { get; set; } = true;

    /// <summary>
    /// Si no hay suelo para un salto corto de esquiva, realiza un salto de evasión para eludir la colisión.
    /// </summary>
    public bool ThreatDodgeJumpEnabled { get; set; } = true;

    /// <summary>
    /// Peso de la cercanía en la prioridad de selección de objetivos entre múltiples enemigos.
    /// </summary>
    public float TargetPriorityDistanceWeight { get; set; } = 0.35f;

    /// <summary>
    /// Peso de la velocidad del enemigo en la prioridad de selección.
    /// </summary>
    public float TargetPrioritySpeedWeight { get; set; } = 0.20f;

    /// <summary>
    /// Peso de la velocidad de aproximación directa hacia Cloth (máxima prioridad para embestidas frontales rápidas).
    /// </summary>
    public float TargetPriorityApproachWeight { get; set; } = 0.35f;

    /// <summary>
    /// Peso de la protección al Caballero (enemigos que cargan hacia el Caballero ganan prioridad).
    /// </summary>
    public float TargetPriorityKnightProtectionWeight { get; set; } = 0.10f;

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

    /// <summary>
    /// Duración en segundos que la hitbox del ataque (y la postura) permanecen activas y haciendo daño una vez asestado el golpe.
    /// </summary>
    public float AttackActiveDuration { get; set; } = 0.25f;

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
    /// Segundos tras el tiempo de hit y la pose de impacto (AttackActiveDuration) antes de volver a "Jump" si no hay suelo.
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
    /// Duración (s) del aturdimiento durante el cual Cloth no puede actuar tras recibir un golpe.
    /// </summary>
    public float HurtStunDuration { get; set; } = 0.22f;

    /// <summary>
    /// Velocidad horizontal inicial del knockback (se aleja del atacante).
    /// </summary>
    public float KnockbackSpeedX { get; set; } = 14f;

    /// <summary>
    /// Velocidad vertical inicial del knockback (elevación).
    /// </summary>
    public float KnockbackSpeedY { get; set; } = 8f;

    /// <summary>
    /// Si está activo, el knockback se calcula según la dirección real del ataque (los golpes hacia abajo aplastan contra el suelo en lugar de elevar).
    /// </summary>
    public bool KnockbackDirectional { get; set; } = true;

    /// <summary>
    /// Escala de gravedad aplicada durante el knockback. Asegura caída ágil con peso real sin flotar.
    /// </summary>
    public float KnockbackGravityScale { get; set; } = 2.2f;

    /// <summary>
    /// Tasa de desaceleración horizontal del knockback para un deslizamiento fluido y reactivo.
    /// </summary>
    public float KnockbackDeceleration { get; set; } = 38f;

    /// <summary>
    /// Duración máxima (s) de la postura/animación de daño antes de recuperar la postura.
    /// </summary>
    public float HurtAnimDuration { get; set; } = 0.22f;

    /// <summary>
    /// Si Cloth toca el suelo tras el golpe, recupera el control de inmediato si terminó el aturdimiento base.
    /// </summary>
    public bool KnockbackRecoverOnLand { get; set; } = true;

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

    /// <summary>
    /// Segundos entre cada reasignación de objetivos de los enemigos hacia Cloth. Menor = la detectan antes (algo más costoso).
    /// </summary>
    public float EnemyRetargetInterval { get; set; } = 0.15f;

    #endregion

    #region Hit Freeze & Slam Damage

    /// <summary>
    /// Segundos que Cloth queda congelada en el aire (o donde esté) justo al recibir un golpe, antes del knockback. 0 = desactivado.
    /// </summary>
    public float HitFreezeDuration { get; set; } = 0.06f;

    /// <summary>
    /// Daño del golpe en el ataque de salto corto (también el encadenado tras el retroceso). SlamDamageAmount es el del salto largo.
    /// </summary>
    public int CloseSlamDamageAmount { get; set; } = 25;

    /// <summary>
    /// Si está activo, tras el salto de retroceso Cloth encadena sí o sí un ataque de salto corto contra el enemigo.
    /// </summary>
    public bool RetreatChainsAttack { get; set; } = true;

    #endregion

    #region Jump Planner

    /// <summary>
    /// Si está activo, Cloth simula cada salto contra el terreno antes de ejecutarlo (y elige variantes, saltos de
    /// posicionamiento o puntos de aproximación seguros). Si se desactiva, usa el comportamiento anterior.
    /// </summary>
    public bool UseJumpPlanner { get; set; } = true;

    /// <summary>
    /// Paso de tiempo (s) de la simulación de trayectoria. Menor = más preciso y más costoso.
    /// </summary>
    public float JumpPlanStep { get; set; } = 0.05f;

    /// <summary>
    /// Escala del círculo de colisión usado en la simulación (1 = tamaño real; menor = más permisivo).
    /// </summary>
    public float JumpPlanRadiusScale { get; set; } = 0.9f;

    /// <summary>
    /// Cantidad de variantes de salto más altas que se prueban si el salto calculado no llega.
    /// </summary>
    public int JumpHeightVariants { get; set; } = 3;

    /// <summary>
    /// Incremento de altura entre variantes (hasta MaxJumpHeight).
    /// </summary>
    public float JumpHeightVariantStep { get; set; } = 1.5f;

    /// <summary>
    /// Error horizontal máximo aceptado entre el aterrizaje simulado y el destino.
    /// </summary>
    public float LandingToleranceX { get; set; } = 3f;

    /// <summary>
    /// Error vertical máximo aceptado entre el aterrizaje simulado y el destino.
    /// </summary>
    public float LandingToleranceY { get; set; } = 2f;

    /// <summary>
    /// Segundos entre intentos de planificar un ataque cuando no hay plan viable.
    /// </summary>
    public float JumpPlanRetryInterval { get; set; } = 0.25f;

    /// <summary>
    /// Si está activo, cuando el salto directo no es viable Cloth se posiciona con un salto corto y luego hace el largo.
    /// El salto corto usa los parámetros de la pestaña "Ataque Corto".
    /// </summary>
    public bool HopPlanningEnabled { get; set; } = true;

    /// <summary>
    /// Separación entre los puntos candidatos de posicionamiento.
    /// </summary>
    public float HopSearchStep { get; set; } = 2f;

    /// <summary>
    /// Distancia máxima a la que se busca un punto de posicionamiento.
    /// </summary>
    public float HopSearchMaxDistance { get; set; } = 8f;

    /// <summary>
    /// Desnivel máximo del punto de posicionamiento respecto a Cloth.
    /// </summary>
    public float HopMaxDeltaY { get; set; } = 2.5f;

    /// <summary>
    /// Distancia al enemigo a la que Cloth se aproxima o emerge (en vez de hacerlo justo debajo de él).
    /// </summary>
    public float ApproachStandoff { get; set; } = 10f;

    #endregion

    #region Escape Jump

    /// <summary>
    /// Si está activo, al recibir muchos golpes seguidos Cloth hace un gran salto para escapar.
    /// </summary>
    public bool EscapeEnabled { get; set; } = true;

    /// <summary>
    /// Golpes recibidos dentro de la ventana de tiempo para activar el escape.
    /// </summary>
    public int EscapeHitCount { get; set; } = 3;

    /// <summary>
    /// Ventana de tiempo (s) en la que se cuentan los golpes.
    /// </summary>
    public float EscapeHitWindow { get; set; } = 4f;

    /// <summary>
    /// Altura del gran salto de escape.
    /// </summary>
    public float EscapeJumpHeight { get; set; } = 14f;

    /// <summary>
    /// Tiempo hasta el ápice del salto de escape.
    /// </summary>
    public float EscapeApexTime { get; set; } = 0.9f;

    /// <summary>
    /// Distancia horizontal objetivo del escape (se prueban también distancias menores).
    /// </summary>
    public float EscapeDistance { get; set; } = 20f;

    /// <summary>
    /// Velocidad horizontal máxima del salto de escape.
    /// </summary>
    public float EscapeSpeedX { get; set; } = 26f;

    /// <summary>
    /// Multiplicador de gravedad al descender en el escape.
    /// </summary>
    public float EscapeDiveMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Invulnerabilidad extra (s) durante el escape.
    /// </summary>
    public float EscapeInvulnerability { get; set; } = 1f;

    /// <summary>
    /// Tiempo mínimo (s) entre escapes.
    /// </summary>
    public float EscapeCooldown { get; set; } = 6f;

    /// <summary>
    /// Duración (s) de "Jump Attack Antic" antes de despegar en el escape.
    /// </summary>
    public float EscapeAnticDuration { get; set; } = 0.1f;

    /// <summary>
    /// El escape solo se activa si a Cloth le queda este porcentaje de vida o menos (100 = con cualquier vida).
    /// Se ignora si la vida máxima es 0 (sin límite).
    /// </summary>
    public int EscapeHealthPercent { get; set; } = 50;

    #endregion

    #region Counter Jump

    /// <summary>
    /// Si está activo, tras recibir golpes seguidos de un enemigo Cloth salta alto hacia arriba y un poco atrás
    /// y cae sobre el enemigo golpeándolo.
    /// </summary>
    public bool CounterEnabled { get; set; } = true;

    /// <summary>
    /// Golpes recibidos dentro de la ventana de tiempo para activar el contraataque.
    /// </summary>
    public int CounterHitCount { get; set; } = 2;

    /// <summary>
    /// Ventana de tiempo (s) en la que se cuentan los golpes del contraataque.
    /// </summary>
    public float CounterHitWindow { get; set; } = 3f;

    /// <summary>
    /// El contraataque solo se activa si a Cloth le queda este porcentaje de vida o menos (100 = con cualquier vida).
    /// </summary>
    public int CounterHealthPercent { get; set; } = 100;

    /// <summary>
    /// Tiempo mínimo (s) entre contraataques.
    /// </summary>
    public float CounterCooldown { get; set; } = 5f;

    /// <summary>
    /// Distancia máxima al enemigo que la golpeó para poder contraatacarlo.
    /// </summary>
    public float CounterMaxDistance { get; set; } = 15f;

    /// <summary>
    /// Altura del salto del contraataque.
    /// </summary>
    public float CounterJumpHeight { get; set; } = 10f;

    /// <summary>
    /// Tiempo hasta el ápice del salto del contraataque.
    /// </summary>
    public float CounterApexTime { get; set; } = 0.75f;

    /// <summary>
    /// Velocidad horizontal hacia atrás durante la subida.
    /// </summary>
    public float CounterBackSpeedX { get; set; } = 6f;

    /// <summary>
    /// Velocidad horizontal máxima durante la caída hacia el enemigo.
    /// </summary>
    public float CounterMaxSpeedX { get; set; } = 28f;

    /// <summary>
    /// Multiplicador de gravedad en la caída del contraataque.
    /// </summary>
    public float CounterDiveMultiplier { get; set; } = 2f;

    /// <summary>
    /// Duración (s) de "Jump Attack Antic" antes de despegar en el contraataque.
    /// </summary>
    public float CounterAnticDuration { get; set; } = 0.1f;

    /// <summary>
    /// Daño del golpe del contraataque.
    /// </summary>
    public int CounterSlamDamageAmount { get; set; } = 50;

    /// <summary>
    /// Invulnerabilidad extra (s) durante el contraataque.
    /// </summary>
    public float CounterInvulnerability { get; set; } = 0.6f;

    #endregion

    #region Knight vs Cloth

    /// <summary>
    /// Si está activo, la hitbox de Cloth daña al Caballero cuando lo toca (también sin modo duelo).
    /// </summary>
    public bool ClothCanHurtKnight { get; set; } = true;

    /// <summary>
    /// Daño (máscaras) que Cloth le hace al Caballero por golpe.
    /// </summary>
    public int ClothDamageToKnight { get; set; } = 1;

    /// <summary>
    /// Si está activo, los golpes de clavo del Caballero dañan a Cloth.
    /// </summary>
    public bool KnightCanHurtCloth { get; set; } = true;

    /// <summary>
    /// Daño (en unidades de la vida máxima de Cloth) que le hace un golpe de clavo del Caballero.
    /// </summary>
    public int KnightHitDamage { get; set; } = 2;

    #endregion

    #region Test Options

    /// <summary>
    /// Si está activo, Cloth considera al Caballero como objetivo de ataque (modo duelo). El daño real depende de ClothCanHurtKnight.
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

    #region Audio & Voice

    /// <summary>
    /// Si está activo, Cloth reproduce sus sonidos originales del juego (gritos de ataque, saltos, daño, etc.).
    /// </summary>
    public bool ClothSoundsEnabled { get; set; } = true;

    /// <summary>
    /// Volumen de reproducción de las voces y efectos de Cloth (de 0.0 a 1.0).
    /// </summary>
    public float ClothVoiceVolume { get; set; } = 0.85f;

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