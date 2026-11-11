using System;
using ClothTheWarriorCompanion.DebugTools;
using UnityEngine;

namespace ClothTheWarriorCompanion.Config;

/// <summary>
/// Ventana flotante de configuración rápida dentro del juego.
/// Permite calibrar y ajustar todas las variables de Cloth en tiempo real
/// durante la partida pulsando la tecla de acceso rápido (por defecto 'O').
/// </summary>
public sealed class ClothQuickMenu : MonoBehaviour {
    private static ClothQuickMenu? _instance;
    private bool _isOpen = false;
    private int _selectedTab = 0;
    private Rect _windowRect = new(60, 60, 560, 560);
    private Vector2 _scrollPosition = Vector2.zero;
    private static Texture2D? _cursorTexture;

    private static readonly string[] TabTitles = { "Seguimiento", "Salto y Física", "Combate", "Hitbox Ataque", "Ataque Corto", "Anim. Ataque", "Retroceso", "Daño y Target", "Planificador", "Escape", "Contraataque", "Predicción", "Duelo" };

    public static bool IsOpen => _instance != null && _instance._isOpen;

    public static void Initialize() {
        if (_instance != null)
            return;
        var hostObject = new GameObject("ClothQuickMenuHost");
        DontDestroyOnLoad(hostObject);
        _instance = hostObject.AddComponent<ClothQuickMenu>();
    }

    public static void Toggle() {
        if (_instance == null) {
            Initialize();
        }
        if (_instance != null) {
            _instance._isOpen = !_instance._isOpen;
            if (_instance._isOpen) {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }

    private void Update() {
        var settings = ClothSettings.Current;
        if (Input.GetKeyDown(settings.QuickMenuKey) || (_isOpen && Input.GetKeyDown(KeyCode.Escape))) {
            Toggle();
        }
    }

    private void LateUpdate() {
        if (_isOpen) {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void OnGUI() {
        if (!_isOpen)
            return;

        GUI.depth = -1000;
        _windowRect = GUI.Window(94821, _windowRect, DrawWindowContent, "Cloth Companion: Configuración Rápida");

        // Dibujar cursor visible encima de todo para garantizar visibilidad en Hollow Knight
        GUI.depth = -2000;
        Vector2 mouse = Event.current.mousePosition;
        GUI.DrawTexture(new Rect(mouse.x, mouse.y, 15, 21), GetCursorTexture());
    }

    private void DrawWindowContent(int windowId) {
        var settings = ClothSettings.Current;

        GUILayout.Space(5);
        _selectedTab = GUILayout.SelectionGrid(_selectedTab, TabTitles, 5, GUILayout.Height(84));
        GUILayout.Space(10);

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        switch (_selectedTab) {
            case 0:
                DrawFollowTab(settings);
                break;
            case 1:
                DrawPhysicsTab(settings);
                break;
            case 2:
                DrawCombatTab(settings);
                break;
            case 3:
                DrawHitboxTab(settings);
                break;
            case 4:
                DrawCloseAttackTab(settings);
                break;
            case 5:
                DrawAttackAnimTab(settings);
                break;
            case 6:
                DrawRetreatTab(settings);
                break;
            case 7:
                DrawDamageTab(settings);
                break;
            case 8:
                DrawPlannerTab(settings);
                break;
            case 9:
                DrawEscapeTab(settings);
                break;
            case 10:
                DrawCounterTab(settings);
                break;
            case 11:
                DrawPredictionTab(settings);
                break;
            case 12:
                DrawTestTab(settings);
                break;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Cerrar (O / Esc)", GUILayout.Height(30))) {
            Toggle();
        }

        if (GUILayout.Button("Valores Predeterminados", GUILayout.Height(30))) {
            ResetToDefaults(settings);
            ClothSettings.Save();
        }

        GUILayout.EndHorizontal();

        // Permite arrastrar la ventana desde el encabezado
        GUI.DragWindow(new Rect(0, 0, _windowRect.width, 25));
    }

    private void DrawFollowTab(ClothSettings settings) {
        DrawFloatRow("Distancia Seguir X:", settings.FollowDistanceX, v => settings.FollowDistanceX = v, 1f, float.MinValue, float.MaxValue);
        DrawFloatRow("Distancia Seguir Y:", settings.FollowDistanceY, v => settings.FollowDistanceY = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Umbral Salto X:", settings.BurrowThresholdX, v => settings.BurrowThresholdX = v, 2f, float.MinValue, float.MaxValue);
        DrawFloatRow("Umbral Salto Enemigo X:", settings.BurrowThresholdEnemyX, v => settings.BurrowThresholdEnemyX = v, 2f, float.MinValue, float.MaxValue);
        DrawFloatRow("Límite Caída (Burrow):", settings.MaxFallDropDeltaY, v => settings.MaxFallDropDeltaY = v, 1f, float.MinValue, float.MaxValue);
        DrawFloatRow("Cooldown tras Salto:", settings.FollowCooldownAfterJump, v => settings.FollowCooldownAfterJump = v, 0.1f, float.MinValue, float.MaxValue, "0.0s");
        DrawFloatRow("Cooldown tras Burrow:", settings.FollowCooldownAfterBurrow, v => settings.FollowCooldownAfterBurrow = v, 0.2f, float.MinValue, float.MaxValue, "0.0s");
        DrawFloatRow("Distancia Rescate Total:", settings.RescueDistanceTotal, v => settings.RescueDistanceTotal = v, 5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Distancia Rescate Y:", settings.RescueDistanceY, v => settings.RescueDistanceY = v, 5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Tiempo Rescate Atasco:", settings.BusyRescueTimeout, v => settings.BusyRescueTimeout = v, 1f, float.MinValue, float.MaxValue, "0.0s");
    }

    private void DrawPhysicsTab(ClothSettings settings) {
        DrawFloatRow("Altura Mínima Salto:", settings.MinJumpHeight, v => settings.MinJumpHeight = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Altura Máxima Salto:", settings.MaxJumpHeight, v => settings.MaxJumpHeight = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Buffer Altura Salto:", settings.JumpHeightBuffer, v => settings.JumpHeightBuffer = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Velocidad Normal Salto X:", settings.JumpNormalMaxSpeedX, v => settings.JumpNormalMaxSpeedX = v, 1f, float.MinValue, float.MaxValue);
        DrawFloatRow("Mult. Gravedad Caída Salto:", settings.JumpFallGravityMultiplier, v => settings.JumpFallGravityMultiplier = v, 0.1f, 1f, 10f, "0.0x");
        DrawFloatRow("Escala Recover Salto:", settings.JumpRecoverDurationScale, v => settings.JumpRecoverDurationScale = v, 0.1f, 0f, 5f, "0.0x");
        DrawFloatRow("Alcance Vertical Máx.:", settings.MaxVerticalReach, v => settings.MaxVerticalReach = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Gravedad en Reposo:", settings.RestGravity, v => settings.RestGravity = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Ápice Mínimo (Tiempo):", settings.MinApexTime, v => settings.MinApexTime = v, 0.05f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Ápice Máximo (Tiempo):", settings.MaxApexTime, v => settings.MaxApexTime = v, 0.05f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Margen Seguridad Techo:", settings.CeilingSafetyMargin, v => settings.CeilingSafetyMargin = v, 0.1f, float.MinValue, float.MaxValue);
        DrawFloatRow("Distancia Suelo permitida:", settings.maxAllowedGroundDistance, v => settings.maxAllowedGroundDistance = v, 0.1f, float.MinValue, float.MaxValue);
    }

    private void DrawCombatTab(ClothSettings settings) {
        DrawFloatRow("Detección de Enemigos:", settings.DetectionRadiusCloth, v => settings.DetectionRadiusCloth = v, 2f, float.MinValue, float.MaxValue);
        DrawFloatRow("Radio Protección Caballero:", settings.ProtectionRadiusHero, v => settings.ProtectionRadiusHero = v, 2f, float.MinValue, float.MaxValue);
        DrawFloatRow("Frecuencia Escaneo:", settings.EnemyScanInterval, v => settings.EnemyScanInterval = v, 0.05f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Rango Inicio Ataque:", settings.AttackRange, v => settings.AttackRange = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Velocidad Ataque X:", settings.AttackMaxSpeedX, v => settings.AttackMaxSpeedX = v, 1f, float.MinValue, float.MaxValue);
        DrawFloatRow("Mult. Gravedad Picado:", settings.DiveGravityMultiplier, v => settings.DiveGravityMultiplier = v, 0.1f, float.MinValue, float.MaxValue);
        DrawFloatRow("Distancia Anim. Ataque:", settings.AttackAnimDistance, v => settings.AttackAnimDistance = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Distancia Golpe Aéreo:", settings.AirHitDistance, v => settings.AirHitDistance = v, 0.5f, float.MinValue, float.MaxValue);
        DrawIntRow("Daño Slam largo (Garrotazo):", settings.SlamDamageAmount, v => settings.SlamDamageAmount = v, 5, int.MinValue, int.MaxValue);
        DrawFloatRow("Cooldown de Ataque:", settings.AttackCooldown, v => settings.AttackCooldown = v, 0.1f, float.MinValue, float.MaxValue, "0.0s");
    }

    private void DrawHitboxTab(ClothSettings settings) {
        GUILayout.Label("Posición relativa a Cloth. El offset X se invierte con su orientación.");
        DrawFloatRow("Offset X (adelante +):", settings.AttackBoxOffsetX, v => settings.AttackBoxOffsetX = v, 0.25f, float.MinValue, float.MaxValue, "0.00");
        DrawFloatRow("Offset Y (arriba +):", settings.AttackBoxOffsetY, v => settings.AttackBoxOffsetY = v, 0.25f, float.MinValue, float.MaxValue, "0.00");
        DrawFloatRow("Ancho:", settings.AttackBoxWidth, v => settings.AttackBoxWidth = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Alto:", settings.AttackBoxHeight, v => settings.AttackBoxHeight = v, 0.5f, float.MinValue, float.MaxValue);

        GUILayout.Space(10);
        string label = ClothVisualDebugger.IsActive ? "Ocultar hitboxes (F1)" : "Mostrar hitboxes (F1)";
        if (GUILayout.Button(label, GUILayout.Height(28))) {
            ClothVisualDebugger.IsActive = !ClothVisualDebugger.IsActive;
        }
    }

    private void DrawCloseAttackTab(ClothSettings settings) {
        GUILayout.Label("Salto corto para enemigos muy cercanos (0 en distancia = desactivado).");
        DrawFloatRow("Distancia activación:", settings.CloseAttackDistance, v => settings.CloseAttackDistance = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Desnivel Y máx.:", settings.CloseAttackMaxDeltaY, v => settings.CloseAttackMaxDeltaY = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Altura del hop:", settings.CloseJumpHeight, v => settings.CloseJumpHeight = v, 0.1f, float.MinValue, float.MaxValue, "0.00");
        DrawFloatRow("Tiempo al ápice:", settings.CloseApexTime, v => settings.CloseApexTime = v, 0.02f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Velocidad X máx.:", settings.CloseAttackSpeedX, v => settings.CloseAttackSpeedX = v, 1f, float.MinValue, float.MaxValue);
        DrawFloatRow("Mult. Gravedad Picado:", settings.CloseDiveGravityMultiplier, v => settings.CloseDiveGravityMultiplier = v, 0.1f, float.MinValue, float.MaxValue);

        GUILayout.Space(8);
        GUILayout.Label("Animación (Antic mientras sube, 'Jump Attack' solo al bajar)");
        DrawFloatRow("Duración Antic:", settings.CloseAnticDuration, v => settings.CloseAnticDuration = v, 0.02f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Anim. antes del golpe (t):", settings.CloseAnimLeadTime, v => settings.CloseAnimLeadTime = v, 0.02f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Anim. por distancia:", settings.CloseAnimDistance, v => settings.CloseAnimDistance = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Distancia Golpe Aéreo:", settings.CloseAirHitDistance, v => settings.CloseAirHitDistance = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Espera chequeo suelo:", settings.CloseLandCheckDelay, v => settings.CloseLandCheckDelay = v, 0.02f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Escala Recover:", settings.CloseRecoverDurationScale, v => settings.CloseRecoverDurationScale = v, 0.1f, float.MinValue, float.MaxValue, "0.0x");
        DrawFloatRow("Cooldown tras ataque:", settings.CloseAttackCooldown, v => settings.CloseAttackCooldown = v, 0.1f, float.MinValue, float.MaxValue, "0.0s");
        DrawIntRow("Daño Slam corto:", settings.CloseSlamDamageAmount, v => settings.CloseSlamDamageAmount = v, 5, 0, 1000);
    }

    private void DrawAttackAnimTab(ClothSettings settings) {
        GUILayout.Label("Pipeline de animación del ataque normal (salto largo).");
        DrawFloatRow("Duración Antic:", settings.AttackAnticDuration, v => settings.AttackAnticDuration = v, 0.02f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Anim. antes del golpe (t):", settings.AttackAnimLeadTime, v => settings.AttackAnimLeadTime = v, 0.05f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Anim. por distancia:", settings.AttackAnimDistance, v => settings.AttackAnimDistance = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Distancia Golpe Aéreo:", settings.AirHitDistance, v => settings.AirHitDistance = v, 0.5f, float.MinValue, float.MaxValue);
        DrawFloatRow("Espera chequeo suelo:", settings.AttackLandCheckDelay, v => settings.AttackLandCheckDelay = v, 0.05f, float.MinValue, float.MaxValue, "0.00s");
        DrawFloatRow("Escala Recover:", settings.RecoverDurationScale, v => settings.RecoverDurationScale = v, 0.1f, float.MinValue, float.MaxValue, "0.0x");
        DrawFloatRow("Cooldown de Ataque:", settings.AttackCooldown, v => settings.AttackCooldown = v, 0.1f, float.MinValue, float.MaxValue, "0.0s");

        GUILayout.Space(8);
        GUILayout.Label("Impacto Activo (Dañar mientras mantenga el golpe)");
        DrawFloatRow("Duración Hitbox Activa:", settings.AttackActiveDuration, v => settings.AttackActiveDuration = v, 0.05f, 0f, 2f, "0.00s");

        GUILayout.Space(8);
        GUILayout.Label("Caída tras el golpe (aplica a los dos ataques: normal y corto).");
        DrawFloatRow("Espera tras el golpe:", settings.PostAttackFallDelay, v => settings.PostAttackFallDelay = v, 0.02f, 0f, 2f, "0.00s");
        DrawIntRow("Frames de 'Jump':", settings.PostAttackJumpFrames, v => settings.PostAttackJumpFrames = v, 1, 0, 30);
        DrawIntRow("Recover desde el frame:", settings.PostAttackFallRecoverStartFrame, v => settings.PostAttackFallRecoverStartFrame = v, 1, 1, 20);
    }

    private void DrawRetreatTab(ClothSettings settings) {
        GUILayout.Label("Salto hacia atrás sin golpe cuando un enemigo está pegado a Cloth (0 en distancia = desactivado).");
        DrawBoolRow("Encadenar ataque de salto corto", settings.RetreatChainsAttack, v => settings.RetreatChainsAttack = v);
        DrawFloatRow("Distancia activación:", settings.RetreatTriggerDistance, v => settings.RetreatTriggerDistance = v, 0.25f, 0f, 30f, "0.00");
        DrawFloatRow("Desnivel Y máx.:", settings.RetreatMaxDeltaY, v => settings.RetreatMaxDeltaY = v, 0.5f, 0f, 30f);
        DrawFloatRow("Distancia de retroceso:", settings.RetreatDistance, v => settings.RetreatDistance = v, 0.5f, 0.5f, 30f);
        DrawFloatRow("Altura del salto:", settings.RetreatJumpHeight, v => settings.RetreatJumpHeight = v, 0.1f, 0.1f, 20f, "0.00");
        DrawFloatRow("Tiempo al ápice:", settings.RetreatApexTime, v => settings.RetreatApexTime = v, 0.02f, 0.05f, 2f, "0.00s");
        DrawFloatRow("Velocidad X máx.:", settings.RetreatSpeedX, v => settings.RetreatSpeedX = v, 1f, 1f, 60f);
        DrawFloatRow("Mult. Gravedad Picado:", settings.RetreatDiveGravityMultiplier, v => settings.RetreatDiveGravityMultiplier = v, 0.1f, 1f, 10f, "0.0x");

        GUILayout.Space(8);
        GUILayout.Label("Animación (Antic durante todo el vuelo, Recover al aterrizar)");
        DrawFloatRow("Duración Antic:", settings.RetreatAnticDuration, v => settings.RetreatAnticDuration = v, 0.02f, 0f, 2f, "0.00s");
        DrawFloatRow("Espera chequeo suelo:", settings.RetreatLandCheckDelay, v => settings.RetreatLandCheckDelay = v, 0.02f, 0f, 2f, "0.00s");
        DrawFloatRow("Escala Recover:", settings.RetreatRecoverDurationScale, v => settings.RetreatRecoverDurationScale = v, 0.1f, 0f, 5f, "0.0x");
        DrawFloatRow("Cooldown entre retrocesos:", settings.RetreatCooldown, v => settings.RetreatCooldown = v, 0.1f, 0f, 30f, "0.0s");
    }

    private void DrawDamageTab(ClothSettings settings) {
        GUILayout.Label("Daño y knockback que recibe Cloth (Vida máxima 0 = sin límite).");
        DrawBoolRow("Cloth recibe daño", settings.CanTakeDamage, v => settings.CanTakeDamage = v);
        DrawIntRow("Vida máxima:", settings.MaxHealth, v => settings.MaxHealth = v, 1, 0, 100);
        DrawFloatRow("Invulnerabilidad tras golpe:", settings.HurtInvulnerability, v => settings.HurtInvulnerability = v, 0.1f, 0f, 10f, "0.0s");
        DrawFloatRow("Duración aturdimiento:", settings.HurtStunDuration, v => settings.HurtStunDuration = v, 0.05f, 0.04f, 3f, "0.00s");
        DrawFloatRow("Duración anim. daño:", settings.HurtAnimDuration, v => settings.HurtAnimDuration = v, 0.05f, 0.04f, 2f, "0.00s");
        DrawFloatRow("Congelación al golpe:", settings.HitFreezeDuration, v => settings.HitFreezeDuration = v, 0.01f, 0f, 1f, "0.00s");
        DrawFloatRow("Knockback X:", settings.KnockbackSpeedX, v => settings.KnockbackSpeedX = v, 2f, 0f, 150f);
        DrawFloatRow("Knockback Y:", settings.KnockbackSpeedY, v => settings.KnockbackSpeedY = v, 1f, 0f, 60f);
        DrawFloatRow("Gravedad de knockback:", settings.KnockbackGravityScale, v => settings.KnockbackGravityScale = v, 0.2f, 0.5f, 10f, "0.0x");
        DrawFloatRow("Desaceleración horizontal:", settings.KnockbackDeceleration, v => settings.KnockbackDeceleration = v, 5f, 5f, 200f);
        DrawBoolRow("Recuperar al tocar suelo", settings.KnockbackRecoverOnLand, v => settings.KnockbackRecoverOnLand = v);
        DrawFloatRow("FPS anim. daño (0 = off):", settings.HurtAnimFps, v => settings.HurtAnimFps = v, 1f, 0f, 60f);
        DrawFloatRow("Retorno tras caer:", settings.DefeatedReturnDelay, v => settings.DefeatedReturnDelay = v, 0.5f, 0f, 60f, "0.0s");

        GUILayout.Space(8);
        GUILayout.Label("Target de enemigos (solo afecta a los que guardan al Caballero en una variable de su FSM).");
        DrawBoolRow("Los enemigos le hacen target", settings.EnemiesTargetCloth, v => settings.EnemiesTargetCloth = v);
        DrawFloatRow("Rango de target:", settings.EnemyTargetRange, v => settings.EnemyTargetRange = v, 2f, 0f, 200f);
        DrawFloatRow("Frecuencia de reasignación:", settings.EnemyRetargetInterval, v => settings.EnemyRetargetInterval = v, 0.05f, 0.05f, 2f, "0.00s");
    }

    private void DrawPlannerTab(ClothSettings settings) {
        GUILayout.Label("Simula cada salto contra el terreno antes de hacerlo (evita saltos que chocan o no llegan).");
        DrawBoolRow("Usar planificador de saltos", settings.UseJumpPlanner, v => settings.UseJumpPlanner = v);
        DrawFloatRow("Paso de simulación:", settings.JumpPlanStep, v => settings.JumpPlanStep = v, 0.01f, 0.02f, 0.2f, "0.00s");
        DrawFloatRow("Escala del círculo:", settings.JumpPlanRadiusScale, v => settings.JumpPlanRadiusScale = v, 0.05f, 0.3f, 1.2f, "0.00x");
        DrawIntRow("Variantes de altura:", settings.JumpHeightVariants, v => settings.JumpHeightVariants = v, 1, 0, 8);
        DrawFloatRow("Incremento por variante:", settings.JumpHeightVariantStep, v => settings.JumpHeightVariantStep = v, 0.25f, 0.1f, 6f, "0.00");
        DrawFloatRow("Tolerancia aterrizaje X:", settings.LandingToleranceX, v => settings.LandingToleranceX = v, 0.25f, 0.25f, 15f, "0.00");
        DrawFloatRow("Tolerancia aterrizaje Y:", settings.LandingToleranceY, v => settings.LandingToleranceY = v, 0.25f, 0.25f, 15f, "0.00");
        DrawFloatRow("Reintento de plan ataque:", settings.JumpPlanRetryInterval, v => settings.JumpPlanRetryInterval = v, 0.05f, 0.05f, 5f, "0.00s");

        GUILayout.Space(8);
        GUILayout.Label("Salto corto de posicionamiento (usa los parámetros de 'Ataque Corto').");
        DrawBoolRow("Posicionarse antes del salto largo", settings.HopPlanningEnabled, v => settings.HopPlanningEnabled = v);
        DrawFloatRow("Separación de candidatos:", settings.HopSearchStep, v => settings.HopSearchStep = v, 0.5f, 0.5f, 10f);
        DrawFloatRow("Distancia máx. de búsqueda:", settings.HopSearchMaxDistance, v => settings.HopSearchMaxDistance = v, 1f, 1f, 30f);
        DrawFloatRow("Desnivel máx. del punto:", settings.HopMaxDeltaY, v => settings.HopMaxDeltaY = v, 0.5f, 0f, 15f);

        GUILayout.Space(8);
        GUILayout.Label("Aproximación a enemigos (también donde emerge al excavar).");
        DrawFloatRow("Distancia al enemigo:", settings.ApproachStandoff, v => settings.ApproachStandoff = v, 1f, 2f, 40f);
    }

    private void DrawEscapeTab(ClothSettings settings) {
        GUILayout.Label("Gran salto para escapar cuando recibe muchos golpes seguidos.");
        DrawBoolRow("Escape activado", settings.EscapeEnabled, v => settings.EscapeEnabled = v);
        DrawIntRow("Golpes para activar:", settings.EscapeHitCount, v => settings.EscapeHitCount = v, 1, 1, 20);
        DrawIntRow("Vida restante máx. (%):", settings.EscapeHealthPercent, v => settings.EscapeHealthPercent = v, 5, 0, 100);
        DrawFloatRow("Ventana de tiempo:", settings.EscapeHitWindow, v => settings.EscapeHitWindow = v, 0.5f, 0.5f, 30f, "0.0s");
        DrawFloatRow("Cooldown de escape:", settings.EscapeCooldown, v => settings.EscapeCooldown = v, 0.5f, 0f, 60f, "0.0s");
        DrawFloatRow("Invulnerabilidad extra:", settings.EscapeInvulnerability, v => settings.EscapeInvulnerability = v, 0.1f, 0f, 10f, "0.0s");

        GUILayout.Space(8);
        GUILayout.Label("Salto de escape (animación 'Jump' y Recover con la escala de Salto y Física)");
        DrawFloatRow("Altura:", settings.EscapeJumpHeight, v => settings.EscapeJumpHeight = v, 0.5f, 1f, 30f);
        DrawFloatRow("Tiempo al ápice:", settings.EscapeApexTime, v => settings.EscapeApexTime = v, 0.05f, 0.1f, 3f, "0.00s");
        DrawFloatRow("Distancia:", settings.EscapeDistance, v => settings.EscapeDistance = v, 1f, 2f, 60f);
        DrawFloatRow("Velocidad X máx.:", settings.EscapeSpeedX, v => settings.EscapeSpeedX = v, 1f, 1f, 80f);
        DrawFloatRow("Mult. Gravedad Picado:", settings.EscapeDiveMultiplier, v => settings.EscapeDiveMultiplier = v, 0.1f, 1f, 10f, "0.0x");
        DrawFloatRow("Duración Antic:", settings.EscapeAnticDuration, v => settings.EscapeAnticDuration = v, 0.02f, 0f, 2f, "0.00s");
    }

    private void DrawCounterTab(ClothSettings settings) {
        GUILayout.Label("Tras golpes seguidos de un enemigo: salto alto hacia arriba y un poco atrás, y cae justo sobre él golpeándolo.");
        DrawBoolRow("Contraataque activado", settings.CounterEnabled, v => settings.CounterEnabled = v);
        DrawIntRow("Golpes para activar:", settings.CounterHitCount, v => settings.CounterHitCount = v, 1, 1, 20);
        DrawFloatRow("Ventana de tiempo:", settings.CounterHitWindow, v => settings.CounterHitWindow = v, 0.5f, 0.5f, 30f, "0.0s");
        DrawIntRow("Vida restante máx. (%):", settings.CounterHealthPercent, v => settings.CounterHealthPercent = v, 5, 0, 100);
        DrawFloatRow("Cooldown:", settings.CounterCooldown, v => settings.CounterCooldown = v, 0.5f, 0f, 60f, "0.0s");
        DrawFloatRow("Distancia máx. al enemigo:", settings.CounterMaxDistance, v => settings.CounterMaxDistance = v, 1f, 2f, 60f);
        DrawFloatRow("Invulnerabilidad extra:", settings.CounterInvulnerability, v => settings.CounterInvulnerability = v, 0.1f, 0f, 10f, "0.0s");

        GUILayout.Space(8);
        GUILayout.Label("Salto");
        DrawFloatRow("Altura:", settings.CounterJumpHeight, v => settings.CounterJumpHeight = v, 0.5f, 1f, 30f);
        DrawFloatRow("Tiempo al ápice:", settings.CounterApexTime, v => settings.CounterApexTime = v, 0.05f, 0.1f, 3f, "0.00s");
        DrawFloatRow("Velocidad hacia atrás X:", settings.CounterBackSpeedX, v => settings.CounterBackSpeedX = v, 1f, 0f, 40f);
        DrawFloatRow("Velocidad caída X máx.:", settings.CounterMaxSpeedX, v => settings.CounterMaxSpeedX = v, 1f, 1f, 80f);
        DrawFloatRow("Mult. Gravedad Picado:", settings.CounterDiveMultiplier, v => settings.CounterDiveMultiplier = v, 0.1f, 1f, 10f, "0.0x");
        DrawFloatRow("Duración Antic:", settings.CounterAnticDuration, v => settings.CounterAnticDuration = v, 0.02f, 0f, 2f, "0.00s");
        DrawIntRow("Daño del golpe:", settings.CounterSlamDamageAmount, v => settings.CounterSlamDamageAmount = v, 5, 0, 1000);
    }

    private void DrawPredictionTab(ClothSettings settings) {
        GUILayout.Label("Predicción Balística Ofensiva:");
        DrawBoolRow("Activar Predicción Ofensiva", settings.PredictTargetMovement, v => settings.PredictTargetMovement = v);
        DrawFloatRow("Tiempo Máx (s) a predecir:", settings.PredictionMaxTime, v => settings.PredictionMaxTime = v, 0.1f, 0f, 5f, "0.0s");
        DrawFloatRow("Distancia Máx de ajuste:", settings.PredictionMaxDistance, v => settings.PredictionMaxDistance = v, 1f, 0f, 50f);
        DrawFloatRow("Margen de seguridad hitbox:", settings.HitboxClearanceMargin, v => settings.HitboxClearanceMargin = v, 0.05f, 0.05f, 3f, "0.00");
        
        GUILayout.Space(8);
        GUILayout.Label("Anticipación Defensiva (TTC / CPA):");
        DrawBoolRow("Predecir ataques y colisiones", settings.DefensivePredictionEnabled, v => settings.DefensivePredictionEnabled = v);
        DrawFloatRow("Ventana anticipación (s):", settings.ThreatLookaheadTime, v => settings.ThreatLookaheadTime = v, 0.1f, 0.2f, 3f, "0.0s");
        DrawFloatRow("Margen cono de peligro:", settings.ThreatMargin, v => settings.ThreatMargin = v, 0.1f, 0.1f, 3f, "0.0");
        DrawBoolRow("Esquiva con salto corto + contra", settings.ThreatDodgeHopEnabled, v => settings.ThreatDodgeHopEnabled = v);
        DrawBoolRow("Esquiva con salto largo", settings.ThreatDodgeJumpEnabled, v => settings.ThreatDodgeJumpEnabled = v);

        GUILayout.Space(8);
        GUILayout.Label("Priorización de Múltiples Objetivos (Pesos):");
        DrawFloatRow("Peso Distancia:", settings.TargetPriorityDistanceWeight, v => settings.TargetPriorityDistanceWeight = v, 0.05f, 0f, 1f, "0.00");
        DrawFloatRow("Peso Velocidad:", settings.TargetPrioritySpeedWeight, v => settings.TargetPrioritySpeedWeight = v, 0.05f, 0f, 1f, "0.00");
        DrawFloatRow("Peso Embestida a Cloth:", settings.TargetPriorityApproachWeight, v => settings.TargetPriorityApproachWeight = v, 0.05f, 0f, 1f, "0.00");
        DrawFloatRow("Peso Protección Caballero:", settings.TargetPriorityKnightProtectionWeight, v => settings.TargetPriorityKnightProtectionWeight = v, 0.05f, 0f, 1f, "0.00");
    }

    private void DrawTestTab(ClothSettings settings) {
        GUILayout.Label("Daño entre Cloth y el Caballero. Cada dirección se activa o desactiva por separado.");
        DrawBoolRow("Cloth puede golpear al Caballero", settings.ClothCanHurtKnight, v => settings.ClothCanHurtKnight = v);
        DrawIntRow("Daño de Cloth al Caballero:", settings.ClothDamageToKnight, v => settings.ClothDamageToKnight = v, 1, 0, 10);
        DrawBoolRow("El Caballero puede golpear a Cloth", settings.KnightCanHurtCloth, v => settings.KnightCanHurtCloth = v);
        DrawIntRow("Daño del Caballero a Cloth:", settings.KnightHitDamage, v => settings.KnightHitDamage = v, 1, 0, 50);

        GUILayout.Space(8);
        GUILayout.Label("Modo duelo (pruebas): base para pelear contra el Caballero.");
        DrawBoolRow("Cloth ataca al Caballero (lo toma como objetivo)", settings.AttackPlayer, v => settings.AttackPlayer = v);
        GUILayout.Label("Con esto activo, Cloth ignora a los enemigos y ataca al Caballero dentro de su radio de detección. Si 'Cloth puede golpear al Caballero' está desactivado, la hitbox se registra pero no hace daño.");

        GUILayout.Space(10);
        GUILayout.Label("Efectos de Sonido y Voz de Cloth (clips originales del juego):");
        DrawBoolRow("Efectos de voz activados", settings.ClothSoundsEnabled, v => settings.ClothSoundsEnabled = v);
        DrawFloatRow("Volumen de voz:", settings.ClothVoiceVolume, v => settings.ClothVoiceVolume = v, 0.05f, 0f, 1f, "0.00");

        GUILayout.Space(10);
        string label = ClothVisualDebugger.IsActive ? "Ocultar hitboxes (F1)" : "Mostrar hitboxes (F1)";
        if (GUILayout.Button(label, GUILayout.Height(28))) {
            ClothVisualDebugger.IsActive = !ClothVisualDebugger.IsActive;
        }
    }

    private static void DrawBoolRow(string label, bool current, Action<bool> setter) {
        bool value = GUILayout.Toggle(current, label);
        if (value != current) {
            setter(value);
            ClothSettings.Save();
        }
    }

    private static void DrawFloatRow(string label, float current, Action<float> setter, float step, float min, float max, string format = "0.0") {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(200));

        if (GUILayout.Button("-", GUILayout.Width(35))) {
            float newValue = Mathf.Clamp((float) Math.Round(current - step, 2), min, max);
            setter(newValue);
            ClothSettings.Save();
        }

        GUILayout.Label(current.ToString(format), GUILayout.Width(60));

        if (GUILayout.Button("+", GUILayout.Width(35))) {
            float newValue = Mathf.Clamp((float) Math.Round(current + step, 2), min, max);
            setter(newValue);
            ClothSettings.Save();
        }

        GUILayout.EndHorizontal();
    }

    private static void DrawIntRow(string label, int current, Action<int> setter, int step, int min, int max) {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(200));

        if (GUILayout.Button("-", GUILayout.Width(35))) {
            int newValue = Mathf.Clamp(current - step, min, max);
            setter(newValue);
            ClothSettings.Save();
        }

        GUILayout.Label(current.ToString(), GUILayout.Width(60));

        if (GUILayout.Button("+", GUILayout.Width(35))) {
            int newValue = Mathf.Clamp(current + step, min, max);
            setter(newValue);
            ClothSettings.Save();
        }

        GUILayout.EndHorizontal();
    }

    private static void ResetToDefaults(ClothSettings settings) {
        var def = new ClothSettings();
        settings.FollowDistanceX = def.FollowDistanceX;
        settings.FollowDistanceY = def.FollowDistanceY;
        settings.BurrowThresholdX = def.BurrowThresholdX;
        settings.BurrowThresholdEnemyX = def.BurrowThresholdEnemyX;
        settings.MaxFallDropDeltaY = def.MaxFallDropDeltaY;
        settings.FollowCooldownAfterJump = def.FollowCooldownAfterJump;
        settings.FollowCooldownAfterBurrow = def.FollowCooldownAfterBurrow;
        settings.JumpNormalMaxSpeedX = def.JumpNormalMaxSpeedX;
        settings.MinJumpHeight = def.MinJumpHeight;
        settings.MaxJumpHeight = def.MaxJumpHeight;
        settings.JumpHeightBuffer = def.JumpHeightBuffer;
        settings.MinApexTime = def.MinApexTime;
        settings.MaxApexTime = def.MaxApexTime;
        settings.RestGravity = def.RestGravity;
        settings.CeilingSafetyMargin = def.CeilingSafetyMargin;
        settings.MaxVerticalReach = def.MaxVerticalReach;
        settings.DetectionRadiusCloth = def.DetectionRadiusCloth;
        settings.ProtectionRadiusHero = def.ProtectionRadiusHero;
        settings.EnemyScanInterval = def.EnemyScanInterval;
        settings.AttackRange = def.AttackRange;
        settings.AttackMaxSpeedX = def.AttackMaxSpeedX;
        settings.DiveGravityMultiplier = def.DiveGravityMultiplier;
        settings.AttackAnimDistance = def.AttackAnimDistance;
        settings.AirHitDistance = def.AirHitDistance;
        settings.SlamDamageAmount = def.SlamDamageAmount;
        settings.AttackCooldown = def.AttackCooldown;
        settings.RescueDistanceTotal = def.RescueDistanceTotal;
        settings.RescueDistanceY = def.RescueDistanceY;
        settings.RescueAbyssY = def.RescueAbyssY;
        settings.BusyRescueTimeout = def.BusyRescueTimeout;
        settings.maxAllowedGroundDistance = def.maxAllowedGroundDistance;

        settings.AttackBoxOffsetX = def.AttackBoxOffsetX;
        settings.AttackBoxOffsetY = def.AttackBoxOffsetY;
        settings.AttackBoxWidth = def.AttackBoxWidth;
        settings.AttackBoxHeight = def.AttackBoxHeight;
        settings.CloseAttackDistance = def.CloseAttackDistance;
        settings.CloseAttackMaxDeltaY = def.CloseAttackMaxDeltaY;
        settings.CloseJumpHeight = def.CloseJumpHeight;
        settings.CloseApexTime = def.CloseApexTime;
        settings.CloseAttackSpeedX = def.CloseAttackSpeedX;
        settings.CloseDiveGravityMultiplier = def.CloseDiveGravityMultiplier;
        settings.CloseAnticDuration = def.CloseAnticDuration;
        settings.CloseAnimLeadTime = def.CloseAnimLeadTime;
        settings.CloseAnimDistance = def.CloseAnimDistance;
        settings.CloseAirHitDistance = def.CloseAirHitDistance;
        settings.CloseLandCheckDelay = def.CloseLandCheckDelay;
        settings.CloseRecoverDurationScale = def.CloseRecoverDurationScale;
        settings.CloseAttackCooldown = def.CloseAttackCooldown;
        
        settings.AttackActiveDuration = def.AttackActiveDuration;
        settings.PredictTargetMovement = def.PredictTargetMovement;
        settings.PredictionMaxTime = def.PredictionMaxTime;
        settings.PredictionMaxDistance = def.PredictionMaxDistance;
        settings.HitboxClearanceMargin = def.HitboxClearanceMargin;
        settings.DefensivePredictionEnabled = def.DefensivePredictionEnabled;
        settings.ThreatLookaheadTime = def.ThreatLookaheadTime;
        settings.ThreatMargin = def.ThreatMargin;
        settings.ThreatDodgeHopEnabled = def.ThreatDodgeHopEnabled;
        settings.ThreatDodgeJumpEnabled = def.ThreatDodgeJumpEnabled;
        settings.TargetPriorityDistanceWeight = def.TargetPriorityDistanceWeight;
        settings.TargetPrioritySpeedWeight = def.TargetPrioritySpeedWeight;
        settings.TargetPriorityApproachWeight = def.TargetPriorityApproachWeight;
        settings.TargetPriorityKnightProtectionWeight = def.TargetPriorityKnightProtectionWeight;

        settings.AttackAnticDuration = def.AttackAnticDuration;
        settings.AttackAnimLeadTime = def.AttackAnimLeadTime;
        settings.AttackLandCheckDelay = def.AttackLandCheckDelay;
        settings.RecoverDurationScale = def.RecoverDurationScale;

        settings.JumpFallGravityMultiplier = def.JumpFallGravityMultiplier;
        settings.JumpRecoverDurationScale = def.JumpRecoverDurationScale;

        settings.PostAttackFallDelay = def.PostAttackFallDelay;
        settings.PostAttackJumpFrames = def.PostAttackJumpFrames;
        settings.PostAttackFallRecoverStartFrame = def.PostAttackFallRecoverStartFrame;

        settings.RetreatTriggerDistance = def.RetreatTriggerDistance;
        settings.RetreatMaxDeltaY = def.RetreatMaxDeltaY;
        settings.RetreatDistance = def.RetreatDistance;
        settings.RetreatJumpHeight = def.RetreatJumpHeight;
        settings.RetreatApexTime = def.RetreatApexTime;
        settings.RetreatSpeedX = def.RetreatSpeedX;
        settings.RetreatDiveGravityMultiplier = def.RetreatDiveGravityMultiplier;
        settings.RetreatAnticDuration = def.RetreatAnticDuration;
        settings.RetreatLandCheckDelay = def.RetreatLandCheckDelay;
        settings.RetreatRecoverDurationScale = def.RetreatRecoverDurationScale;
        settings.RetreatCooldown = def.RetreatCooldown;

        settings.CanTakeDamage = def.CanTakeDamage;
        settings.MaxHealth = def.MaxHealth;
        settings.HurtInvulnerability = def.HurtInvulnerability;
        settings.HurtStunDuration = def.HurtStunDuration;
        settings.HurtAnimDuration = def.HurtAnimDuration;
        settings.KnockbackSpeedX = def.KnockbackSpeedX;
        settings.KnockbackSpeedY = def.KnockbackSpeedY;
        settings.KnockbackGravityScale = def.KnockbackGravityScale;
        settings.KnockbackDeceleration = def.KnockbackDeceleration;
        settings.KnockbackRecoverOnLand = def.KnockbackRecoverOnLand;
        settings.HurtAnimFps = def.HurtAnimFps;
        settings.KnockbackDirectional = def.KnockbackDirectional;
        settings.DodgeClearanceMargin = def.DodgeClearanceMargin;
        settings.DodgeMinDistance = def.DodgeMinDistance;
        settings.ClothSoundsEnabled = def.ClothSoundsEnabled;
        settings.ClothVoiceVolume = def.ClothVoiceVolume;
        settings.DefeatedReturnDelay = def.DefeatedReturnDelay;
        settings.EnemiesTargetCloth = def.EnemiesTargetCloth;
        settings.EnemyTargetRange = def.EnemyTargetRange;

        settings.UseJumpPlanner = def.UseJumpPlanner;
        settings.JumpPlanStep = def.JumpPlanStep;
        settings.JumpPlanRadiusScale = def.JumpPlanRadiusScale;
        settings.JumpHeightVariants = def.JumpHeightVariants;
        settings.JumpHeightVariantStep = def.JumpHeightVariantStep;
        settings.LandingToleranceX = def.LandingToleranceX;
        settings.LandingToleranceY = def.LandingToleranceY;
        settings.JumpPlanRetryInterval = def.JumpPlanRetryInterval;
        settings.HopPlanningEnabled = def.HopPlanningEnabled;
        settings.HopSearchStep = def.HopSearchStep;
        settings.HopSearchMaxDistance = def.HopSearchMaxDistance;
        settings.HopMaxDeltaY = def.HopMaxDeltaY;
        settings.ApproachStandoff = def.ApproachStandoff;

        settings.EscapeEnabled = def.EscapeEnabled;
        settings.EscapeHitCount = def.EscapeHitCount;
        settings.EscapeHitWindow = def.EscapeHitWindow;
        settings.EscapeJumpHeight = def.EscapeJumpHeight;
        settings.EscapeApexTime = def.EscapeApexTime;
        settings.EscapeDistance = def.EscapeDistance;
        settings.EscapeSpeedX = def.EscapeSpeedX;
        settings.EscapeDiveMultiplier = def.EscapeDiveMultiplier;
        settings.EscapeInvulnerability = def.EscapeInvulnerability;
        settings.EscapeCooldown = def.EscapeCooldown;
        settings.EscapeAnticDuration = def.EscapeAnticDuration;

        settings.EnemyRetargetInterval = def.EnemyRetargetInterval;
        settings.ClothCanHurtKnight = def.ClothCanHurtKnight;
        settings.ClothDamageToKnight = def.ClothDamageToKnight;
        settings.KnightCanHurtCloth = def.KnightCanHurtCloth;
        settings.KnightHitDamage = def.KnightHitDamage;

        settings.HitFreezeDuration = def.HitFreezeDuration;
        settings.CloseSlamDamageAmount = def.CloseSlamDamageAmount;
        settings.RetreatChainsAttack = def.RetreatChainsAttack;
        settings.EscapeHealthPercent = def.EscapeHealthPercent;

        settings.CounterEnabled = def.CounterEnabled;
        settings.CounterHitCount = def.CounterHitCount;
        settings.CounterHitWindow = def.CounterHitWindow;
        settings.CounterHealthPercent = def.CounterHealthPercent;
        settings.CounterCooldown = def.CounterCooldown;
        settings.CounterMaxDistance = def.CounterMaxDistance;
        settings.CounterJumpHeight = def.CounterJumpHeight;
        settings.CounterApexTime = def.CounterApexTime;
        settings.CounterBackSpeedX = def.CounterBackSpeedX;
        settings.CounterMaxSpeedX = def.CounterMaxSpeedX;
        settings.CounterDiveMultiplier = def.CounterDiveMultiplier;
        settings.CounterAnticDuration = def.CounterAnticDuration;
        settings.CounterSlamDamageAmount = def.CounterSlamDamageAmount;
        settings.CounterInvulnerability = def.CounterInvulnerability;

        settings.AttackPlayer = def.AttackPlayer;
    }

    private static Texture2D GetCursorTexture() {
        if (_cursorTexture != null)
            return _cursorTexture;

        int w = 15;
        int h = 21;
        _cursorTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        _cursorTexture.filterMode = FilterMode.Point;

        string[] pattern = {
            "100000000000000",
            "110000000000000",
            "121000000000000",
            "122100000000000",
            "122210000000000",
            "122221000000000",
            "122222100000000",
            "122222210000000",
            "122222221000000",
            "122222222100000",
            "122222222210000",
            "122222111111000",
            "122122100000000",
            "121012210000000",
            "110001221000000",
            "100000122100000",
            "000000012210000",
            "000000001221000",
            "000000000110000",
            "000000000000000",
            "000000000000000"
        };

        Color black = new Color(0.1f, 0.1f, 0.1f, 1f);
        Color white = Color.white;
        Color clear = Color.clear;

        for (int y = 0; y < h; y++) {
            string row = pattern[y];
            int texY = (h - 1) - y;
            for (int x = 0; x < w; x++) {
                char c = x < row.Length ? row[x] : '0';
                Color pixelColor = c == '1' ? black : (c == '2' ? white : clear);
                _cursorTexture.SetPixel(x, texY, pixelColor);
            }
        }

        _cursorTexture.Apply();
        return _cursorTexture;
    }
}