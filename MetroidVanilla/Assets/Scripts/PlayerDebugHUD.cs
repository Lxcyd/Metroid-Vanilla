using UnityEngine;
using System.Reflection;

// ================================================================
//   PLAYER DEBUG HUD  (v3)
//   FIX GRAPHES : OnRenderObject ne fonctionne que si le GameObject
//   a un Renderer. Le joueur n'en a pas (Rigidbody2D + Collider).
//   Solution : Camera.onPostRender → garanti d'être appelé.
//
//   Raccourcis (Play Mode) :
//     H  → afficher / masquer
//     1  → panneau Vélocité & États
//     2  → panneau Graphes
// ================================================================

[RequireComponent(typeof(PlayerController))]
public class PlayerDebugHUD : MonoBehaviour
{
    [Header("━━━━━━━━━━  DEBUG HUD  ━━━━━━━━━━━━━━━━━━━━━")]
    public bool showHUD = true;
    public bool showVelocity = true;
    public bool showGraph = true;

    [Range(0.5f, 2f)]
    public float uiScale = 1f;

    [Tooltip("Lissage des valeurs affichées. 0.08 = stable, 0.5 = réactif")]
    [Range(0.01f, 1f)]
    public float displaySmoothing = 0.08f;

    // ── Valeurs lissées ──────────────────────────────────────────
    private float _smoothVelX;
    private float _smoothVelY;
    private float _smoothSpeed;

    // ── Graphe ───────────────────────────────────────────────────
    private const int GRAPH_SAMPLES = 180;
    private float[] _velYHistory = new float[GRAPH_SAMPLES];
    private float[] _velXHistory = new float[GRAPH_SAMPLES];
    private int _graphHead = 0;
    private float _graphTimer = 0f;
    private const float GRAPH_RATE = 1f / 30f;

    // Rects calculés dans OnGUI, lus dans le callback caméra
    private Rect _graphRectY;
    private Rect _graphRectX;
    private bool _graphRectsReady = false;

    // ── Matériau GL ──────────────────────────────────────────────
    private Material _lineMat;

    // ── Refs ─────────────────────────────────────────────────────
    private PlayerController _pc;
    private Rigidbody2D _rb;

    // ── Styles GUI ───────────────────────────────────────────────
    private GUIStyle _styleLabel;
    private GUIStyle _styleSmall;
    private bool _stylesInit = false;

    // ── Réflexion champs privés ──────────────────────────────────
    private FieldInfo _fiGrounded, _fiIsJumping, _fiAtApex, _fiWallSliding,
                      _fiIsDashing, _fiOnWall, _fiJumpCut,
                      _fiCoyoteTimer, _fiJumpBufferTimer,
                      _fiDashCooldownTimer, _fiWallJumpTimer,
                      _fiAirJumpsLeft, _fiDashesLeft;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _rb = GetComponent<Rigidbody2D>();

        // Réflexion
        var t = typeof(PlayerController);
        var f = BindingFlags.NonPublic | BindingFlags.Instance;
        _fiGrounded = t.GetField("_grounded", f);
        _fiIsJumping = t.GetField("_isJumping", f);
        _fiAtApex = t.GetField("_atApex", f);
        _fiWallSliding = t.GetField("_wallSliding", f);
        _fiIsDashing = t.GetField("_isDashing", f);
        _fiOnWall = t.GetField("_onWall", f);
        _fiJumpCut = t.GetField("_jumpCut", f);
        _fiCoyoteTimer = t.GetField("_coyoteTimer", f);
        _fiJumpBufferTimer = t.GetField("_jumpBufferTimer", f);
        _fiDashCooldownTimer = t.GetField("_dashCooldownTimer", f);
        _fiWallJumpTimer = t.GetField("_wallJumpTimer", f);
        _fiAirJumpsLeft = t.GetField("_airJumpsLeft", f);
        _fiDashesLeft = t.GetField("_dashesLeft", f);

        // Matériau pour GL
        _lineMat = new Material(Shader.Find("Hidden/Internal-Colored"));
        _lineMat.hideFlags = HideFlags.HideAndDontSave;
        _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMat.SetInt("_ZWrite", 0);
    }

    // FIX : on s'abonne à Camera.onPostRender (fonctionne sans Renderer)
    private void OnEnable()
    {
        Camera.onPostRender += DrawGL;
    }

    private void OnDisable()
    {
        Camera.onPostRender -= DrawGL;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) showHUD = !showHUD;
        if (Input.GetKeyDown(KeyCode.Alpha1)) showVelocity = !showVelocity;
        if (Input.GetKeyDown(KeyCode.Alpha2)) showGraph = !showGraph;

        // Lissage frame-rate indépendant
        float alpha = 1f - Mathf.Pow(1f - displaySmoothing, Time.deltaTime * 60f);
        Vector2 rv = _rb.linearVelocity;
        _smoothVelX = Mathf.Lerp(_smoothVelX, rv.x, alpha);
        _smoothVelY = Mathf.Lerp(_smoothVelY, rv.y, alpha);
        _smoothSpeed = Mathf.Lerp(_smoothSpeed, rv.magnitude, alpha);

        // Échantillonnage 30 Hz
        _graphTimer -= Time.deltaTime;
        if (_graphTimer <= 0f)
        {
            _graphTimer = GRAPH_RATE;
            _velYHistory[_graphHead] = rv.y;
            _velXHistory[_graphHead] = rv.x;
            _graphHead = (_graphHead + 1) % GRAPH_SAMPLES;
        }
    }

    // ── IMGUI ─────────────────────────────────────────────────────

    private void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

        Texture2D bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.04f, 0.04f, 0.07f, 0.92f));
        bg.Apply();

        _styleLabel = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(12 * uiScale),
            richText = true
        };
        _styleLabel.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        _styleSmall = new GUIStyle(_styleLabel)
        {
            fontSize = Mathf.RoundToInt(10 * uiScale)
        };
        _styleSmall.normal.textColor = new Color(0.55f, 0.58f, 0.65f);
    }

    private void OnGUI()
    {
        if (!showHUD) return;
        InitStyles();

        _graphRectsReady = false;

        float s = uiScale;
        float x1 = 10f;
        float x2 = x1 + 250f * s;

        GUI.Label(new Rect(x1, 10f, 500f, 18f * s),
            "<color=#4FC3F7><b>PLAYER DEBUG HUD</b></color>  " +
            "<color=#404040>[H] toggle  [1] states  [2] graphs</color>",
            _styleLabel);

        float y = 30f * s;
        if (showVelocity) DrawVelocityPanel(x1, y, s);
        if (showGraph) DrawGraphPanel(x2, y, s);

        _graphRectsReady = showGraph && showHUD;
    }

    // ── PANNEAU ÉTATS ─────────────────────────────────────────────

    private void DrawVelocityPanel(float x, float y, float s)
    {
        float panW = 240f * s;
        float panH = 310f * s;

        // Fond
        GUI.DrawTexture(new Rect(x, y, panW, panH), MakeTex(new Color(0.04f, 0.04f, 0.07f, 0.92f)));
        DrawBorder(x, y, panW, panH, new Color(0.2f, 0.25f, 0.35f));

        float ix = x + 10f * s;
        float iy = y + 8f * s;
        float lh = 19f * s;
        float bw = panW - 20f * s;

        GUI.Label(new Rect(ix, iy, panW, lh),
            "<color=#4FC3F7><b>VÉLOCITÉ & ÉTATS</b></color>", _styleLabel);
        iy += lh + 1f;

        // Valeurs numériques lissées
        string vxCol = Mathf.Abs(_smoothVelX) > _pc.maxSpeed * 0.9f ? "#FF7043" : "#AEFF80";
        string vyCol = _smoothVelY > 0f ? "#80D8FF" : "#FF8A65";

        GUI.Label(new Rect(ix, iy, panW, lh),
            $"Vel X  <color={vxCol}><b>{_smoothVelX:+0.0;-0.0; 0.0}</b></color>  u/s", _styleLabel);
        iy += lh;
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"Vel Y  <color={vyCol}><b>{_smoothVelY:+0.0;-0.0; 0.0}</b></color>  u/s", _styleLabel);
        iy += lh;
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"Speed  <color=#FFD740><b>{_smoothSpeed:0.0}</b></color>  u/s", _styleLabel);
        iy += lh + 2f;

        // Barres
        DrawBar(ix, iy, bw, 8f * s, Mathf.Abs(_smoothVelX) / _pc.runTopSpeed,
            _smoothVelX >= 0f ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.5f, 0.3f));
        iy += 12f * s;

        float normY = Mathf.Clamp01((_smoothVelY + _pc.maxFallSpeed)
                    / (_pc.maxFallSpeed + Mathf.Sqrt(2f * _pc.gravity * _pc.jumpHeight)));
        DrawBar(ix, iy, bw, 8f * s, normY, new Color(0.3f, 0.7f, 1f));
        iy += 14f * s;

        DrawLine(ix, iy, bw, new Color(0.2f, 0.22f, 0.3f));
        iy += 5f * s;

        // États booléens
        GUI.Label(new Rect(ix, iy, panW, lh), "<color=#546E7A><b>ÉTATS</b></color>", _styleSmall);
        iy += lh - 4f;

        bool grounded = Get<bool>(_fiGrounded);
        bool isJumping = Get<bool>(_fiIsJumping);
        bool atApex = Get<bool>(_fiAtApex);
        bool wallSliding = Get<bool>(_fiWallSliding);
        bool isDashing = Get<bool>(_fiIsDashing);
        bool onWall = Get<bool>(_fiOnWall);
        bool jumpCut = Get<bool>(_fiJumpCut);

        iy = DrawBoolRow(ix, iy, panW, lh, s,
            ("Grounded", grounded, "#69F0AE", "#263238"),
            ("Jumping", isJumping, "#80D8FF", "#263238"),
            ("Apex", atApex, "#FFD740", "#263238"));
        iy = DrawBoolRow(ix, iy, panW, lh, s,
            ("JumpCut", jumpCut, "#FF7043", "#263238"),
            ("WallSlide", wallSliding, "#CE93D8", "#263238"),
            ("OnWall", onWall, "#80CBC4", "#263238"));
        iy = DrawBoolRow(ix, iy, panW, lh, s,
            ("Dashing", isDashing, "#FF6E40", "#263238"),
            ("DblJump", _pc.doubleJumpEnabled, "#B9F6CA", "#263238"),
            ("Dash ON", _pc.dashEnabled, "#FFCCBC", "#263238"));

        iy += 3f * s;
        DrawLine(ix, iy, bw, new Color(0.2f, 0.22f, 0.3f));
        iy += 5f * s;

        // Timers
        GUI.Label(new Rect(ix, iy, panW, lh), "<color=#546E7A><b>TIMERS</b></color>", _styleSmall);
        iy += lh - 4f;

        float coyote = Get<float>(_fiCoyoteTimer);
        float jumpBuf = Get<float>(_fiJumpBufferTimer);
        float dashCD = Get<float>(_fiDashCooldownTimer);
        float wallT = Get<float>(_fiWallJumpTimer);
        int airJ = Get<int>(_fiAirJumpsLeft);
        int dashes = Get<int>(_fiDashesLeft);

        iy = DrawTimerBar(ix, iy, bw, lh, s, "Coyote", coyote, _pc.coyoteTime, new Color(1f, 0.8f, 0.2f));
        iy = DrawTimerBar(ix, iy, bw, lh, s, "JumpBuf", jumpBuf, _pc.jumpBufferTime, new Color(0.4f, 1f, 0.4f));
        iy = DrawTimerBar(ix, iy, bw, lh, s, "DashCD", -dashCD, _pc.dashCooldown, new Color(1f, 0.4f, 0.3f));
        iy = DrawTimerBar(ix, iy, bw, lh, s, "WallLock", wallT, _pc.wallJumpLock, new Color(0.6f, 0.8f, 1f));

        iy += 2f * s;
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"AirJumps: <b>{airJ}</b>   Dashes: <b>{dashes}</b>", _styleSmall);
    }

    // ── PANNEAU GRAPHES (fonds seulement — GL dessinera par-dessus) ─

    private void DrawGraphPanel(float x, float y, float s)
    {
        float panW = 280f * s;
        float panH = 230f * s;

        GUI.DrawTexture(new Rect(x, y, panW, panH), MakeTex(new Color(0.04f, 0.04f, 0.07f, 0.92f)));
        DrawBorder(x, y, panW, panH, new Color(0.2f, 0.25f, 0.35f));

        float ix = x + 10f * s;
        float iy = y + 8f * s;
        float lh = 16f * s;
        float gw = panW - 20f * s;
        float gh = 78f * s;

        GUI.Label(new Rect(ix, iy, panW, lh),
            "<color=#4FC3F7><b>GRAPHES VÉLOCITÉ</b></color>", _styleLabel);
        iy += lh + 1f;

        // Graphe Vel Y
        float vJump = Mathf.Sqrt(2f * _pc.gravity * _pc.jumpHeight);
        GUI.Label(new Rect(ix, iy, gw, lh),
            $"<color=#80D8FF>Vel Y</color>  " +
            $"<color=#eee><b>{_rb.linearVelocity.y:+0.0;-0.0}</b></color>  " +
            $"<color=#333>[{-_pc.maxFallSpeed:0} → +{vJump:0.0}]</color>",
            _styleSmall);
        iy += lh;
        // Fond du graphe
        GUI.DrawTexture(new Rect(ix, iy, gw, gh), MakeTex(new Color(0.06f, 0.06f, 0.10f)));
        DrawBorder(ix, iy, gw, gh, new Color(0.2f, 0.22f, 0.3f));
        _graphRectY = new Rect(ix, iy, gw, gh);
        iy += gh + 9f * s;

        // Graphe Vel X
        GUI.Label(new Rect(ix, iy, gw, lh),
            $"<color=#AEFF80>Vel X</color>  " +
            $"<color=#eee><b>{_rb.linearVelocity.x:+0.0;-0.0}</b></color>  " +
            $"<color=#333>[±{_pc.runTopSpeed:0.0}]</color>",
            _styleSmall);
        iy += lh;
        GUI.DrawTexture(new Rect(ix, iy, gw, gh), MakeTex(new Color(0.06f, 0.06f, 0.10f)));
        DrawBorder(ix, iy, gw, gh, new Color(0.2f, 0.22f, 0.3f));
        _graphRectX = new Rect(ix, iy, gw, gh);
    }

    // ── GL CALLBACK ───────────────────────────────────────────────
    // FIX : Camera.onPostRender est appelé après chaque rendu caméra,
    // indépendamment de la présence d'un Renderer sur le GameObject.

    private void DrawGL(Camera cam)
    {
        // On ne dessine que pour la caméra principale, et seulement si
        // les rects sont prêts (OnGUI a tourné ce frame)
        if (!_graphRectsReady || _lineMat == null) return;
        if (cam != Camera.main) return;

        _lineMat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix();    // coordonnées en pixels écran

        float vJump = Mathf.Sqrt(2f * _pc.gravity * _pc.jumpHeight);

        DrawGLGraph(_graphRectY, _velYHistory, _graphHead,
            -_pc.maxFallSpeed, vJump,
            new Color(0.25f, 0.72f, 1f, 1f));

        DrawGLGraph(_graphRectX, _velXHistory, _graphHead,
            -_pc.runTopSpeed * 1.15f, _pc.runTopSpeed * 1.15f,
            new Color(0.45f, 1f, 0.45f, 1f));

        GL.PopMatrix();
    }

    private void DrawGLGraph(Rect r, float[] data, int head,
                              float yMin, float yMax, Color col)
    {
        int n = data.Length;
        float range = yMax - yMin;
        if (range <= 0f) return;

        // Ligne zéro (grise)
        float zNorm = Mathf.Clamp01((0f - yMin) / range);
        float zy = r.y + r.height - zNorm * r.height;

        GL.Begin(GL.LINES);
        GL.Color(new Color(0.35f, 0.37f, 0.48f, 0.9f));
        GL.Vertex3(r.x, zy, 0f);
        GL.Vertex3(r.x + r.width, zy, 0f);
        GL.End();

        // Courbe principale en LINE_STRIP
        GL.Begin(GL.LINE_STRIP);
        GL.Color(col);
        for (int i = 0; i < n; i++)
        {
            int idx = (head + i) % n;
            float t = Mathf.Clamp01((data[idx] - yMin) / range);
            float px = r.x + (float)i / (n - 1) * r.width;
            float py = r.y + r.height - t * r.height;
            GL.Vertex3(px, py, 0f);
        }
        GL.End();
    }

    // ── HELPERS IMGUI ─────────────────────────────────────────────

    private void DrawBar(float x, float y, float w, float h, float t, Color fill)
    {
        t = Mathf.Clamp01(t);
        GUI.DrawTexture(new Rect(x, y, w, h), MakeTex(new Color(0.10f, 0.10f, 0.15f)));
        if (t > 0f)
            GUI.DrawTexture(new Rect(x, y, w * t, h), MakeTex(Color.Lerp(fill * 0.45f, fill, t)));
        GUI.DrawTexture(new Rect(x, y, w, 1), MakeTex(new Color(0.25f, 0.27f, 0.35f)));
        GUI.DrawTexture(new Rect(x, y + h - 1, w, 1), MakeTex(new Color(0.25f, 0.27f, 0.35f)));
    }

    private float DrawTimerBar(float x, float y, float w, float lh, float s,
                                string label, float val, float max, Color col)
    {
        float t = max > 0f ? Mathf.Clamp01(val / max) : 0f;
        float lw = 55f * s;
        GUI.Label(new Rect(x, y, lw, lh),
            $"<color=#455A64>{label}</color>", _styleSmall);
        DrawBar(x + lw, y + 3f * s, w - lw - 40f * s, 7f * s, t, col);
        GUI.Label(new Rect(x + w - 38f * s, y, 38f * s, lh),
            $"<color=#78909C>{Mathf.Max(val, 0f):0.000}s</color>", _styleSmall);
        return y + lh;
    }

    private float DrawBoolRow(float x, float y, float panW, float lh, float s,
        (string n, bool v, string on, string off) a,
        (string n, bool v, string on, string off) b,
        (string n, bool v, string on, string off) c)
    {
        float cw = (panW - 20f * s) / 3f;
        DrawBoolTag(x, y, cw, lh, a.n, a.v, a.on, a.off);
        DrawBoolTag(x + cw, y, cw, lh, b.n, b.v, b.on, b.off);
        DrawBoolTag(x + cw * 2, y, cw, lh, c.n, c.v, c.on, c.off);
        return y + lh;
    }

    private void DrawBoolTag(float x, float y, float w, float h,
                              string name, bool val, string onCol, string offCol)
    {
        GUI.Label(new Rect(x, y, w, h),
            $"<color={(val ? onCol : offCol)}>{(val ? "●" : "○")} {name}</color>",
            _styleSmall);
    }

    private void DrawLine(float x, float y, float w, Color col)
        => GUI.DrawTexture(new Rect(x, y, w, 1), MakeTex(col));

    private void DrawBorder(float x, float y, float w, float h, Color col)
    {
        GUI.DrawTexture(new Rect(x, y, w, 1), MakeTex(col));
        GUI.DrawTexture(new Rect(x, y + h - 1, w, 1), MakeTex(col));
        GUI.DrawTexture(new Rect(x, y, 1, h), MakeTex(col));
        GUI.DrawTexture(new Rect(x + w - 1, y, 1, h), MakeTex(col));
    }

    // Cache textures 1×1
    private static System.Collections.Generic.Dictionary<Color, Texture2D> _texCache
        = new System.Collections.Generic.Dictionary<Color, Texture2D>();

    private static Texture2D MakeTex(Color c)
    {
        if (_texCache.TryGetValue(c, out var t)) return t;
        t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        _texCache[c] = t;
        return t;
    }

    private T Get<T>(FieldInfo fi)
    {
        if (fi == null) return default;
        return (T)fi.GetValue(_pc);
    }
}