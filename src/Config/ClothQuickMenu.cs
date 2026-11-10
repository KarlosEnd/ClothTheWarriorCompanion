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
    private Rect _windowRect = new(60, 60, 480, 560);
    private Vector2 _scrollPosition = Vector2.zero;
    private static Texture2D? _cursorTexture;

    private static readonly string[] TabTitles = { "Seguimiento", "Salto y Física", "Combate" };

    public static bool IsOpen => _instance != null && _instance._isOpen;

    public static void Initialize() {
        if (_instance != null) return;
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
        if (!_isOpen) return;

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
        _selectedTab = GUILayout.Toolbar(_selectedTab, TabTitles, GUILayout.Height(30));
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
        DrawFloatRow("Distancia Seguir X:", settings.FollowDistanceX, v => settings.FollowDistanceX = v, 1f, 2f, 25f);
        DrawFloatRow("Distancia Seguir Y:", settings.FollowDistanceY, v => settings.FollowDistanceY = v, 0.5f, 1f, 15f);
        DrawFloatRow("Umbral Salto X:", settings.BurrowThresholdX, v => settings.BurrowThresholdX = v, 2f, 10f, 50f);
        DrawFloatRow("Umbral Salto Enemigo X:", settings.BurrowThresholdEnemyX, v => settings.BurrowThresholdEnemyX = v, 2f, 10f, 50f);
        DrawFloatRow("Límite Caída (Burrow):", settings.MaxFallDropDeltaY, v => settings.MaxFallDropDeltaY = v, 1f, -30f, -5f);
        DrawFloatRow("Cooldown tras Salto:", settings.FollowCooldownAfterJump, v => settings.FollowCooldownAfterJump = v, 0.1f, 0.1f, 3f, "0.0s");
        DrawFloatRow("Cooldown tras Burrow:", settings.FollowCooldownAfterBurrow, v => settings.FollowCooldownAfterBurrow = v, 0.2f, 0.2f, 4f, "0.0s");
        DrawFloatRow("Distancia Rescate Total:", settings.RescueDistanceTotal, v => settings.RescueDistanceTotal = v, 5f, 20f, 100f);
        DrawFloatRow("Distancia Rescate Y:", settings.RescueDistanceY, v => settings.RescueDistanceY = v, 5f, 15f, 60f);
        DrawFloatRow("Tiempo Rescate Atasco:", settings.BusyRescueTimeout, v => settings.BusyRescueTimeout = v, 1f, 3f, 20f, "0.0s");
    }

    private void DrawPhysicsTab(ClothSettings settings) {
        DrawFloatRow("Altura Mínima Salto:", settings.MinJumpHeight, v => settings.MinJumpHeight = v, 0.5f, 1f, 8f);
        DrawFloatRow("Altura Máxima Salto:", settings.MaxJumpHeight, v => settings.MaxJumpHeight = v, 0.5f, 4f, 18f);
        DrawFloatRow("Buffer Altura Salto:", settings.JumpHeightBuffer, v => settings.JumpHeightBuffer = v, 0.5f, 0f, 6f);
        DrawFloatRow("Velocidad Normal Salto X:", settings.JumpNormalMaxSpeedX, v => settings.JumpNormalMaxSpeedX = v, 1f, 8f, 40f);
        DrawFloatRow("Alcance Vertical Máx.:", settings.MaxVerticalReach, v => settings.MaxVerticalReach = v, 0.5f, 3f, 15f);
        DrawFloatRow("Gravedad en Reposo:", settings.RestGravity, v => settings.RestGravity = v, 0.5f, 1f, 6f);
        DrawFloatRow("Ápice Mínimo (Tiempo):", settings.MinApexTime, v => settings.MinApexTime = v, 0.05f, 0.2f, 1f, "0.00s");
        DrawFloatRow("Ápice Máximo (Tiempo):", settings.MaxApexTime, v => settings.MaxApexTime = v, 0.05f, 0.4f, 1.5f, "0.00s");
        DrawFloatRow("Margen Seguridad Techo:", settings.CeilingSafetyMargin, v => settings.CeilingSafetyMargin = v, 0.1f, 0.1f, 1.5f);
    }

    private void DrawCombatTab(ClothSettings settings) {
        DrawFloatRow("Detección de Enemigos:", settings.DetectionRadiusCloth, v => settings.DetectionRadiusCloth = v, 2f, 10f, 50f);
        DrawFloatRow("Radio Protección Caballero:", settings.ProtectionRadiusHero, v => settings.ProtectionRadiusHero = v, 2f, 10f, 60f);
        DrawFloatRow("Frecuencia Escaneo:", settings.EnemyScanInterval, v => settings.EnemyScanInterval = v, 0.05f, 0.1f, 1.5f, "0.00s");
        DrawFloatRow("Rango Inicio Ataque:", settings.AttackRange, v => settings.AttackRange = v, 1f, 8f, 35f);
        DrawFloatRow("Velocidad Ataque X:", settings.AttackMaxSpeedX, v => settings.AttackMaxSpeedX = v, 1f, 10f, 45f);
        DrawFloatRow("Mult. Gravedad Picado:", settings.DiveGravityMultiplier, v => settings.DiveGravityMultiplier = v, 0.2f, 1f, 5f);
        DrawFloatRow("Distancia Anim. Ataque:", settings.AttackAnimDistance, v => settings.AttackAnimDistance = v, 0.5f, 2f, 12f);
        DrawFloatRow("Distancia Golpe Aéreo:", settings.AirHitDistance, v => settings.AirHitDistance = v, 0.5f, 1f, 8f);
        DrawIntRow("Daño Slam (Garrotazo):", settings.SlamDamageAmount, v => settings.SlamDamageAmount = v, 5, 5, 250);
        DrawFloatRow("Radio de Impacto Slam:", settings.SlamRadius, v => settings.SlamRadius = v, 0.5f, 2f, 15f);
        DrawFloatRow("Cooldown de Ataque:", settings.AttackCooldown, v => settings.AttackCooldown = v, 0.1f, 0.2f, 4f, "0.0s");
    }

    private static void DrawFloatRow(string label, float current, Action<float> setter, float step, float min, float max, string format = "0.0") {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(200));

        if (GUILayout.Button("-", GUILayout.Width(35))) {
            float newValue = Mathf.Clamp((float)Math.Round(current - step, 2), min, max);
            setter(newValue);
            ClothSettings.Save();
        }

        GUILayout.Label(current.ToString(format), GUILayout.Width(60));

        if (GUILayout.Button("+", GUILayout.Width(35))) {
            float newValue = Mathf.Clamp((float)Math.Round(current + step, 2), min, max);
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
        settings.SlamRadius = def.SlamRadius;
        settings.AttackCooldown = def.AttackCooldown;
        settings.RescueDistanceTotal = def.RescueDistanceTotal;
        settings.RescueDistanceY = def.RescueDistanceY;
        settings.RescueAbyssY = def.RescueAbyssY;
        settings.BusyRescueTimeout = def.BusyRescueTimeout;
    }

    private static Texture2D GetCursorTexture() {
        if (_cursorTexture != null) return _cursorTexture;

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
