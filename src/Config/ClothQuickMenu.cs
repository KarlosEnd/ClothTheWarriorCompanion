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

    private static readonly string[] TabTitles = { "Seguimiento", "Salto y Física", "Combate", "Hitbox Ataque", "Ataque Corto", "Anim. Ataque", "Retroceso", "Daño y Target", "Pruebas" };

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
        _selectedTab = GUILayout.SelectionGrid(_selectedTab, TabTitles, 5, GUILayout.Height(56));
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
        DrawIntRow("Daño Slam (Garrotazo):", settings.SlamDamageAmount, v => settings.SlamDamageAmount = v, 5, int.MinValue, int.MaxValue); // Actualizado también para consistencia
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
        GUILayout.Label("Caída tras el golpe (aplica a los dos ataques: normal y corto).");
        DrawFloatRow("Espera tras el golpe:", settings.PostAttackFallDelay, v => settings.PostAttackFallDelay = v, 0.02f, 0f, 2f, "0.00s");
        DrawIntRow("Frames de 'Jump':", settings.PostAttackJumpFrames, v => settings.PostAttackJumpFrames = v, 1, 0, 30);
        DrawIntRow("Recover desde el frame:", settings.PostAttackFallRecoverStartFrame, v => settings.PostAttackFallRecoverStartFrame = v, 1, 1, 20);
    }

    private void DrawRetreatTab(ClothSettings settings) {
        GUILayout.Label("Salto hacia atrás sin golpe cuando un enemigo está pegado a Cloth (0 en distancia = desactivado).");
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
        DrawFloatRow("Duración aturdimiento:", settings.HurtStunDuration, v => settings.HurtStunDuration = v, 0.05f, 0.05f, 3f, "0.00s");
        DrawFloatRow("Knockback X:", settings.KnockbackSpeedX, v => settings.KnockbackSpeedX = v, 1f, 0f, 60f);
        DrawFloatRow("Knockback Y:", settings.KnockbackSpeedY, v => settings.KnockbackSpeedY = v, 1f, 0f, 60f);
        DrawFloatRow("FPS anim. daño (0 = off):", settings.HurtAnimFps, v => settings.HurtAnimFps = v, 1f, 0f, 60f);
        DrawFloatRow("Retorno tras caer:", settings.DefeatedReturnDelay, v => settings.DefeatedReturnDelay = v, 0.5f, 0f, 60f, "0.0s");

        GUILayout.Space(8);
        GUILayout.Label("Target de enemigos (solo afecta a los que guardan al Caballero en una variable de su FSM).");
        DrawBoolRow("Los enemigos le hacen target", settings.EnemiesTargetCloth, v => settings.EnemiesTargetCloth = v);
        DrawFloatRow("Rango de target:", settings.EnemyTargetRange, v => settings.EnemyTargetRange = v, 2f, 0f, 200f);
    }

    private void DrawTestTab(ClothSettings settings) {
        GUILayout.Label("Opciones de prueba (base para un futuro modo duelo).");
        DrawBoolRow("Cloth ataca al Caballero (sin daño)", settings.AttackPlayer, v => settings.AttackPlayer = v);
        GUILayout.Label("Con esto activo, Cloth ignora a los enemigos y ataca al Caballero dentro de su radio de detección. La hitbox se registra pero no hace daño.");

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
        settings.KnockbackSpeedX = def.KnockbackSpeedX;
        settings.KnockbackSpeedY = def.KnockbackSpeedY;
        settings.HurtAnimFps = def.HurtAnimFps;
        settings.DefeatedReturnDelay = def.DefeatedReturnDelay;
        settings.EnemiesTargetCloth = def.EnemiesTargetCloth;
        settings.EnemyTargetRange = def.EnemyTargetRange;

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
