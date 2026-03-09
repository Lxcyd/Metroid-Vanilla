using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;

// ================================================================
//   PLAYER DEBUG HUD  (v6)
//   - Graphes supprimés
//   - Panneau HEAD HITTER complet avec accumulation visible
//   - Momentum dash en air affiché
// ================================================================

[RequireComponent(typeof(PlayerController))]
public class PlayerDebugHUD : MonoBehaviour
{
    [Header("━━━━━━━━━━  DEBUG HUD  ━━━━━━━━━━━━━━━━━━━━━")]
    public bool showHUD      = true;
    public bool showVelocity = true;
    public bool showHeadHit  = true;

    [Range(0.5f, 2f)]
    public float uiScale = 1f;

    [Tooltip("Lissage des valeurs affichées. 0.08 = stable, 0.5 = réactif")]
    [Range(0.01f, 1f)]
    public float displaySmoothing = 0.08f;

    // ── Valeurs lissées ──────────────────────────────────────────
    private float _smoothVelX;
    private float _smoothVelY;
    private float _smoothSpeed;

    // ── Flash bump ───────────────────────────────────────────────
    private float _bumpFlashTimer    = 0f;
    private float _prevCapTimer      = 0f;
    private int   _bumpCount         = 0;
    private float _bumpCountReset    = 0f;
    private float _peakSpeed         = 0f;

    // ── Refs ─────────────────────────────────────────────────────
    private PlayerController _pc;
    private Rigidbody2D      _rb;

    // ── Styles GUI ───────────────────────────────────────────────
    private GUIStyle _styleLabel;
    private GUIStyle _styleSmall;
    private bool     _stylesInit = false;

    // ── Réflexion champs privés ──────────────────────────────────
    private FieldInfo _fiGrounded, _fiIsJumping, _fiAtApex, _fiWallSliding,
                      _fiIsDashing, _fiOnWall, _fiJumpCut,
                      _fiCoyoteTimer, _fiJumpBufferTimer,
                      _fiDashCooldownTimer, _fiWallJumpTimer,
                      _fiAirJumpsLeft, _fiDashesLeft,
                      _fiHeadBumpSpeedCap, _fiHeadBumpCapTimer;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _rb = GetComponent<Rigidbody2D>();

        var t = typeof(PlayerController);
        var f = BindingFlags.NonPublic | BindingFlags.Instance;
        _fiGrounded          = t.GetField("_grounded",          f);
        _fiIsJumping         = t.GetField("_isJumping",         f);
        _fiAtApex            = t.GetField("_atApex",            f);
        _fiWallSliding       = t.GetField("_wallSliding",       f);
        _fiIsDashing         = t.GetField("_isDashing",         f);
        _fiOnWall            = t.GetField("_onWall",            f);
        _fiJumpCut           = t.GetField("_jumpCut",           f);
        _fiCoyoteTimer       = t.GetField("_coyoteTimer",       f);
        _fiJumpBufferTimer   = t.GetField("_jumpBufferTimer",   f);
        _fiDashCooldownTimer = t.GetField("_dashCooldownTimer", f);
        _fiWallJumpTimer     = t.GetField("_wallJumpTimer",     f);
        _fiAirJumpsLeft      = t.GetField("_airJumpsLeft",      f);
        _fiDashesLeft        = t.GetField("_dashesLeft",        f);
        _fiHeadBumpSpeedCap  = t.GetField("_headBumpSpeedCap",  f);
        _fiHeadBumpCapTimer  = t.GetField("_headBumpCapTimer",  f);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.hKey.wasPressedThisFrame)      showHUD      = !showHUD;
            if (kb.digit1Key.wasPressedThisFrame) showVelocity = !showVelocity;
            if (kb.digit2Key.wasPressedThisFrame) showHeadHit  = !showHeadHit;
        }

        float alpha = 1f - Mathf.Pow(1f - displaySmoothing, Time.deltaTime * 60f);
        Vector2 rv  = _rb.linearVelocity;
        _smoothVelX  = Mathf.Lerp(_smoothVelX,  rv.x,         alpha);
        _smoothVelY  = Mathf.Lerp(_smoothVelY,  rv.y,         alpha);
        _smoothSpeed = Mathf.Lerp(_smoothSpeed, rv.magnitude, alpha);

        // Bump flash + compteur consécutifs
        float curCapTimer = Get<float>(_fiHeadBumpCapTimer);
        if (curCapTimer > _prevCapTimer + 0.05f)
        {
            _bumpFlashTimer = 0.25f;
            _bumpCount++;
            _bumpCountReset = 2f;
            float capNow = Get<float>(_fiHeadBumpSpeedCap);
            if (capNow > _peakSpeed) _peakSpeed = capNow;
        }
        _prevCapTimer    = curCapTimer;
        _bumpFlashTimer -= Time.deltaTime;
        _bumpCountReset -= Time.deltaTime;
        if (_bumpCountReset <= 0f) { _bumpCount = 0; _peakSpeed = 0f; }
    }

    // ── IMGUI ─────────────────────────────────────────────────────

    private void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

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

        float s  = uiScale;
        float x1 = 10f;
        float x2 = x1 + 250f * s;

        GUI.Label(new Rect(x1, 10f, 500f, 18f * s),
            "<color=#4FC3F7><b>PLAYER DEBUG HUD</b></color>  " +
            "<color=#404040>[H] toggle  [1] states  [2] headhitter</color>",
            _styleLabel);

        float y = 30f * s;
        if (showVelocity) DrawVelocityPanel(x1, y, s);
        if (showHeadHit)  DrawHeadHitterPanel(x2, y, s);
    }

    // ── PANNEAU ÉTATS ─────────────────────────────────────────────

    private void DrawVelocityPanel(float x, float y, float s)
    {
        float panW = 240f * s;
        float panH = 315f * s;

        GUI.DrawTexture(new Rect(x, y, panW, panH), MakeTex(new Color(0.04f, 0.04f, 0.07f, 0.92f)));
        DrawBorder(x, y, panW, panH, new Color(0.2f, 0.25f, 0.35f));

        float ix = x + 10f * s;
        float iy = y + 8f * s;
        float lh = 19f * s;
        float bw = panW - 20f * s;

        GUI.Label(new Rect(ix, iy, panW, lh),
            "<color=#4FC3F7><b>VÉLOCITÉ & ÉTATS</b></color>", _styleLabel);
        iy += lh + 1f;

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

        DrawBar(ix, iy, bw, 8f * s,
            Mathf.Abs(_smoothVelX) / _pc.runTopSpeed,
            _smoothVelX >= 0f ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.5f, 0.3f));
        iy += 12f * s;

        float normY = Mathf.Clamp01((_smoothVelY + _pc.maxFallSpeed)
                    / (_pc.maxFallSpeed + Mathf.Sqrt(2f * _pc.gravity * _pc.jumpHeight)));
        DrawBar(ix, iy, bw, 8f * s, normY, new Color(0.3f, 0.7f, 1f));
        iy += 14f * s;

        DrawLine(ix, iy, bw, new Color(0.2f, 0.22f, 0.3f));
        iy += 5f * s;

        GUI.Label(new Rect(ix, iy, panW, lh), "<color=#546E7A><b>ÉTATS</b></color>", _styleSmall);
        iy += lh - 4f;

        bool grounded    = Get<bool>(_fiGrounded);
        bool isJumping   = Get<bool>(_fiIsJumping);
        bool atApex      = Get<bool>(_fiAtApex);
        bool wallSliding = Get<bool>(_fiWallSliding);
        bool isDashing   = Get<bool>(_fiIsDashing);
        bool onWall      = Get<bool>(_fiOnWall);
        bool jumpCut     = Get<bool>(_fiJumpCut);

        iy = DrawBoolRow(ix, iy, panW, lh, s,
            ("Grounded",  grounded,              "#69F0AE", "#263238"),
            ("Jumping",   isJumping,             "#80D8FF", "#263238"),
            ("Apex",      atApex,                "#FFD740", "#263238"));
        iy = DrawBoolRow(ix, iy, panW, lh, s,
            ("JumpCut",   jumpCut,               "#FF7043", "#263238"),
            ("WallSlide", wallSliding,           "#CE93D8", "#263238"),
            ("OnWall",    onWall,                "#80CBC4", "#263238"));
        iy = DrawBoolRow(ix, iy, panW, lh, s,
            ("Dashing",   isDashing,             "#FF6E40", "#263238"),
            ("DblJump",   _pc.doubleJumpEnabled, "#B9F6CA", "#263238"),
            ("Dash ON",   _pc.dashEnabled,       "#FFCCBC", "#263238"));

        iy += 3f * s;
        DrawLine(ix, iy, bw, new Color(0.2f, 0.22f, 0.3f));
        iy += 5f * s;

        GUI.Label(new Rect(ix, iy, panW, lh), "<color=#546E7A><b>TIMERS</b></color>", _styleSmall);
        iy += lh - 4f;

        float coyote  = Get<float>(_fiCoyoteTimer);
        float jumpBuf = Get<float>(_fiJumpBufferTimer);
        float dashCD  = Get<float>(_fiDashCooldownTimer);
        float wallT   = Get<float>(_fiWallJumpTimer);
        int   airJ    = Get<int>(_fiAirJumpsLeft);
        int   dashes  = Get<int>(_fiDashesLeft);

        iy = DrawTimerBar(ix, iy, bw, lh, s, "Coyote",   coyote,  _pc.coyoteTime,     new Color(1f, 0.8f, 0.2f));
        iy = DrawTimerBar(ix, iy, bw, lh, s, "JumpBuf",  jumpBuf, _pc.jumpBufferTime, new Color(0.4f, 1f, 0.4f));
        iy = DrawTimerBar(ix, iy, bw, lh, s, "DashCD",  -dashCD,  _pc.dashCooldown,   new Color(1f, 0.4f, 0.3f));
        iy = DrawTimerBar(ix, iy, bw, lh, s, "WallLock", wallT,   _pc.wallJumpLock,   new Color(0.6f, 0.8f, 1f));

        iy += 2f * s;
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"AirJumps: <b>{airJ}</b>   Dashes: <b>{dashes}</b>", _styleSmall);
    }

    // ── PANNEAU HEAD HITTER ───────────────────────────────────────

    private void DrawHeadHitterPanel(float x, float y, float s)
    {
        float panW = 260f * s;
        float panH = 290f * s;

        bool  flashing   = _bumpFlashTimer > 0f;
        float ft         = Mathf.Clamp01(_bumpFlashTimer / 0.25f);
        Color bgCol      = flashing
            ? Color.Lerp(new Color(0.04f, 0.04f, 0.07f, 0.92f), new Color(0.18f, 0.06f, 0.02f, 0.96f), ft)
            : new Color(0.04f, 0.04f, 0.07f, 0.92f);
        Color borderCol  = flashing
            ? Color.Lerp(new Color(0.2f, 0.25f, 0.35f), new Color(1f, 0.4f, 0.1f), ft)
            : new Color(0.2f, 0.25f, 0.35f);

        GUI.DrawTexture(new Rect(x, y, panW, panH), MakeTex(bgCol));
        DrawBorder(x, y, panW, panH, borderCol);

        float ix = x + 10f * s;
        float iy = y + 8f * s;
        float lh = 18f * s;
        float bw = panW - 20f * s;

        // ── Titre ─────────────────────────────────────────────────
        string bumpTag = flashing
            ? $"  <color=#FF5722><b>● BUMP +{_pc.headBumpBonusSpeed:0.0}</b></color>"
            : "";
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"<color=#FFD54F><b>HEAD HITTER</b></color>{bumpTag}", _styleLabel);
        iy += lh + 2f;

        float capSpeed  = Get<float>(_fiHeadBumpSpeedCap);
        float capTimer  = Get<float>(_fiHeadBumpCapTimer);
        bool  capActive = capTimer > 0f;

        // ── Compteur bumps consécutifs + peak ─────────────────────
        string cntCol = _bumpCount >= 4 ? "#FF5722" : _bumpCount >= 2 ? "#FFD740" : "#69F0AE";
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"Bumps  <color={cntCol}><b>×{_bumpCount}</b></color>" +
            $"   Peak  <color=#FF9800><b>{_peakSpeed:0.0}</b></color> u/s",
            _styleLabel);
        iy += lh + 3f;

        // ── Barre d'accumulation (cap vs headBumpMaxSpeed) ─────────
        float maxRange  = Mathf.Max(_pc.headBumpMaxSpeed, 0.01f);
        float fillRatio = capSpeed / maxRange;
        float velRatio  = Mathf.Abs(_rb.linearVelocity.x) / maxRange;
        float topRatio  = _pc.runTopSpeed / maxRange;

        Color barCol = capActive
            ? Color.Lerp(new Color(0.3f, 0.9f, 0.3f), new Color(1f, 0.45f, 0.05f), fillRatio)
            : new Color(0.15f, 0.17f, 0.22f);

        DrawBar(ix, iy, bw, 12f * s, fillRatio, barCol);
        // marqueur vel.x réelle = blanc
        float mxVel = ix + Mathf.Clamp01(velRatio) * bw;
        GUI.DrawTexture(new Rect(mxVel - 1f, iy - 2f, 2f, 16f * s), MakeTex(new Color(1f, 1f, 1f, 0.85f)));
        // marqueur runTopSpeed = jaune
        float mxTop = ix + Mathf.Clamp01(topRatio) * bw;
        GUI.DrawTexture(new Rect(mxTop - 1f, iy, 1f, 12f * s), MakeTex(new Color(1f, 0.9f, 0.2f, 0.55f)));
        iy += 16f * s;

        GUI.Label(new Rect(ix, iy, bw, lh - 4f),
            $"<color=#37474F>cap {capSpeed:0.0} u/s" +
            $"  │  runTop {_pc.runTopSpeed:0.0}  │  max {_pc.headBumpMaxSpeed:0.0}</color>",
            _styleSmall);
        iy += lh;

        // ── Cap actif + timer de protection ──────────────────────
        string capCol = capActive ? "#FFD740" : "#455A64";
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"Cap  <color={capCol}><b>{capSpeed:0.0}</b></color> u/s" +
            $"  <color=#263238>{(capActive ? "● protégé" : "○ expiré")}</color>",
            _styleLabel);
        iy += lh;

        iy = DrawTimerBar(ix, iy, bw, lh, s, "Protect",
            capTimer, _pc.headBumpCapDuration,
            capActive ? new Color(1f, 0.75f, 0.1f) : new Color(0.2f, 0.22f, 0.3f));
        iy += 4f * s;

        DrawLine(ix, iy, bw, new Color(0.2f, 0.22f, 0.3f));
        iy += 5f * s;

        // ── Vel X abs vs seuil min ────────────────────────────────
        float absVelX  = Mathf.Abs(_rb.linearVelocity.x);
        bool  aboveMin = absVelX >= _pc.headBumpMinSpeed;
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"Vel X abs  <color={(aboveMin ? "#AEFF80" : "#FF5252")}><b>{absVelX:0.0}</b></color> u/s" +
            $"  <color=#37474F>min:{_pc.headBumpMinSpeed:0.0}  +{_pc.headBumpBonusSpeed:0.0}/bump</color>",
            _styleSmall);
        iy += lh + 2f;

        // ── Flags ─────────────────────────────────────────────────
        bool isJumping = Get<bool>(_fiIsJumping);
        DrawBoolRow(ix, iy, panW, lh, s,
            ("Enabled", _pc.headHitterEnabled, "#FFD740", "#455A64"),
            ("Jumping", isJumping,             "#80D8FF", "#263238"),
            ("Cap ON",  capActive,             "#FF9800", "#455A64"));
        iy += lh + 4f;

        DrawLine(ix, iy, bw, new Color(0.2f, 0.22f, 0.3f));
        iy += 5f * s;

        // ── Params live ───────────────────────────────────────────
        GUI.Label(new Rect(ix, iy, panW, lh),
            "<color=#546E7A><b>PARAMS</b></color>", _styleSmall);
        iy += lh - 3f;
        GUI.Label(new Rect(ix, iy, panW, lh),
            $"<color=#37474F>+{_pc.headBumpBonusSpeed:0.0} u/s/bump  " +
            $"coyote {_pc.headBumpCoyoteTime:0.00}s  " +
            $"protect {_pc.headBumpCapDuration:0.00}s  " +
            $"max {_pc.headBumpMaxSpeed:0.0}</color>",
            _styleSmall);
    }

    // ── HELPERS IMGUI ─────────────────────────────────────────────

    private void DrawBar(float x, float y, float w, float h, float t, Color fill)
    {
        t = Mathf.Clamp01(t);
        GUI.DrawTexture(new Rect(x, y, w, h), MakeTex(new Color(0.10f, 0.10f, 0.15f)));
        if (t > 0f)
            GUI.DrawTexture(new Rect(x, y, w * t, h), MakeTex(Color.Lerp(fill * 0.45f, fill, t)));
        GUI.DrawTexture(new Rect(x, y,         w, 1), MakeTex(new Color(0.25f, 0.27f, 0.35f)));
        GUI.DrawTexture(new Rect(x, y + h - 1, w, 1), MakeTex(new Color(0.25f, 0.27f, 0.35f)));
    }

    private float DrawTimerBar(float x, float y, float w, float lh, float s,
                                string label, float val, float max, Color col)
    {
        float t  = max > 0f ? Mathf.Clamp01(val / max) : 0f;
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
        DrawBoolTag(x,          y, cw, lh, a.n, a.v, a.on, a.off);
        DrawBoolTag(x + cw,     y, cw, lh, b.n, b.v, b.on, b.off);
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
        GUI.DrawTexture(new Rect(x, y,         w, 1), MakeTex(col));
        GUI.DrawTexture(new Rect(x, y + h - 1, w, 1), MakeTex(col));
        GUI.DrawTexture(new Rect(x, y,         1, h), MakeTex(col));
        GUI.DrawTexture(new Rect(x + w - 1, y, 1, h), MakeTex(col));
    }

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