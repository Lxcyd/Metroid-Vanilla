using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //   ■ CAPACITÉS
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  CAPACITÉS DÉBLOQUÉES  ━━━━━━━━━━")]
    public bool wallMechanicsEnabled = false;
    public bool doubleJumpEnabled = false;
    public bool dashEnabled = false;

    // ══════════════════════════════════════════════════════════════
    //   ■ HORIZONTAL
    //
    //   MOMENTUM MINECRAFT :
    //   Dans Minecraft, sauter en courant conserve presque
    //   intégralement la vitesse horizontale dans l'air.
    //   airDecel ≈ 1.5  →  il faut ~5 secondes pour s'arrêter
    //                       en l'air sans toucher de mur.
    //   Résultat : un saut en sprint couvre ~2× plus de distance
    //   qu'un saut sur place. Exactement comme Minecraft.
    //
    //   airAccel = 18  →  on peut encore changer de direction
    //                      en l'air, mais c'est lent et coûteux.
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  HORIZONTAL  ━━━━━━━━━━━━━━━━━━━━")]
    public float maxSpeed = 8f;
    public float runTopSpeed = 9.5f;

    [Tooltip("Accélération sol (~0.21s pour atteindre maxSpeed)")]
    public float groundAccel = 38f;

    [Tooltip("Décélération sol (franche, ~0.15s)")]
    public float groundDecel = 55f;

    [Tooltip("Accélération air (faible : changer de direction en l'air est difficile)")]
    public float airAccel = 18f;

    [Tooltip("Décélération air. TRÈS faible = momentum Minecraft (~5s pour s'arrêter)")]
    public float airDecel = 1.5f;

    [Range(1f, 4f)]
    public float turnBoost = 2f;

    private const float VEL_DEADZONE = 0.05f;

    [Header("• Dynamic Acceleration Curves")]
    public AnimationCurve groundAccelCurve = AnimationCurve.EaseInOut(0f, 1.2f, 1f, 0.8f);
    public AnimationCurve airAccelCurve = AnimationCurve.EaseInOut(0f, 1.0f, 1f, 0.7f);
    public AnimationCurve decelCurve = AnimationCurve.EaseInOut(0f, 0.8f, 1f, 1.2f);

    // ══════════════════════════════════════════════════════════════
    //   ■ SAUT & GRAVITÉ
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  SAUT & GRAVITÉ  ━━━━━━━━━━━━━━━━")]
    public float jumpHeight = 3.5f;
    public float gravity = 30f;

    [Header("• Variable Gravity — Jump Arcs")]
    [Range(0.1f, 1f)] public float riseGravityMult = 0.55f;
    [Range(1f, 5f)] public float fallGravityMult = 3.0f;
    [Range(1f, 12f)] public float jumpCutMult = 6.0f;
    [Range(0f, 1f)] public float apexGravityMult = 0.80f;
    public float apexThreshold = 0.8f;
    public float maxFallSpeed = 22f;

    [Range(0, 3)] public int airJumps = 0;
    [Range(0.5f, 1f)] public float airJumpMult = 0.9f;

    // ══════════════════════════════════════════════════════════════
    //   ■ FEEL
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  FEEL & RESPONSIVENESS  ━━━━━━━━━")]
    [Range(0f, 0.2f)] public float jumpBufferTime = 0.08f;
    [Range(0f, 0.2f)] public float coyoteTime = 0.08f;

    [Header("• Corner Correction")]
    public bool cornerCorrection = true;
    [Range(0.05f, 0.6f)] public float cornerCorrDist = 0.25f;
    [Range(2, 8)] public int cornerCorrRays = 4;

    [Header("• Ledge Forgiveness")]
    public bool ledgeForgiveness = true;
    [Range(0.05f, 0.4f)] public float ledgeForgiveRange = 0.15f;

    [Header("• Input Latency Reduction")]
    [Range(0f, 0.05f)] public float inputDebounce = 0.016f;

    [Header("• Subpixel Movement")]
    public bool subpixelMovement = true;
    public float pixelSize = 0.0625f;

    [Header("• Squash & Stretch")]
    public bool squashStretch = true;
    [Range(0f, 0.4f)] public float squashIntensity = 0.10f;

    // ══════════════════════════════════════════════════════════════
    //   ■ WALL
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  WALL JUMP / WALL SLIDE  ━━━━━━━━")]
    public float wallJumpForceX = 9.5f;
    [Range(0f, 0.5f)] public float wallJumpLock = 0.15f;
    [Range(0f, 0.8f)] public float wallSlideGrav = 0.12f;

    // ══════════════════════════════════════════════════════════════
    //   ■ DASH
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  DASH  ━━━━━━━━━━━━━━━━━━━━━━━━━━")]
    public float dashSpeed = 17f;
    [Range(0.05f, 0.3f)] public float dashDuration = 0.09f;
    [Range(0.1f, 2f)] public float dashCooldown = 0.6f;
    [Range(0f, 2f)] public float dashExitMomentum = 1.0f;

    // ══════════════════════════════════════════════════════════════
    //   ■ COLLISION DETECTION
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  COLLISION DETECTION  ━━━━━━━━━━━")]
    public LayerMask groundLayer;
    public Vector2 groundCheckOffset = new Vector2(0f, -0.34f);
    public Vector2 groundCheckSize = new Vector2(0.18f, 0.05f);
    public Vector2 wallCheckOffR = new Vector2(0.13f, 0f);
    public Vector2 wallCheckOffL = new Vector2(-0.13f, 0f);
    public Vector2 wallCheckSize = new Vector2(0.05f, 0.28f);
    public float colliderHalfH = 0.33638f;
    public float colliderHalfW = 0.11302f;

    // ══════════════════════════════════════════════════════════════
    //   ■ DEBUG
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  DEBUG  ━━━━━━━━━━━━━━━━━━━━━━━━━")]
    public bool debugPosition = true;
    public bool debugVerbose = false;

    private float _debugTimer;
    private const float DEBUG_INTERVAL = 0.25f;

    // ══════════════════════════════════════════════════════════════
    //   ÉTAT INTERNE
    // ══════════════════════════════════════════════════════════════

    private Rigidbody2D _rb;
    private Vector2 _vel;

    private bool _grounded, _wasGrounded;
    private bool _onWall;
    private float _wallDir;

    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _wallJumpTimer;
    private float _dashCooldownTimer;

    private bool _isJumping;
    private bool _jumpCut;
    private int _airJumpsLeft;
    private bool _atApex;
    private bool _wallSliding;

    private bool _isDashing;
    private float _dashTimer;
    private int _dashesLeft;
    private float _dashDirX;

    private float _inputX;
    private bool _jumpPressedThisFrame;
    private bool _jumpHeld;
    private bool _jumpReleasedThisFrame;
    private bool _dashPressedThisFrame;
    private float _debounceTimer;

    private Vector2 _subpixelAccum;
    private Vector3 _baseScale;
    private Vector3 _targetScale;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _baseScale = transform.localScale;
        _targetScale = _baseScale;
    }

    private void Start()
    {
        _airJumpsLeft = airJumps;
        _dashesLeft = 1;
    }

    private void Update()
    {
        _inputX = Input.GetAxisRaw("Horizontal");
        _jumpHeld = Input.GetButton("Jump");
        _debounceTimer -= Time.deltaTime;

        if (Input.GetButtonDown("Jump") && _debounceTimer <= 0f)
        {
            _jumpPressedThisFrame = true;
            _debounceTimer = inputDebounce;
            _jumpBufferTimer = jumpBufferTime;
        }

        if (Input.GetButtonUp("Jump"))
            _jumpReleasedThisFrame = true;

        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            && _debounceTimer <= 0f)
        {
            _dashPressedThisFrame = true;
            _debounceTimer = inputDebounce;
        }

        if (_jumpReleasedThisFrame && _isJumping && _vel.y > 0f)
            _jumpCut = true;

        if (squashStretch) UpdateSquashStretch();
        DebugLog();
    }

    private void FixedUpdate()
    {
        _vel = _rb.linearVelocity;

        CheckCollisions();
        TickTimers();

        if (_isDashing)
        {
            TickDashMotion();
        }
        else
        {
            HandleGravity();
            HandleHorizontal();
            HandleJump();
            HandleWallSlide();
        }

        TryDash();
        ApplyVelocity();

        _jumpPressedThisFrame = false;
        _jumpReleasedThisFrame = false;
        _dashPressedThisFrame = false;
    }

    // ── 1. COLLISIONS ─────────────────────────────────────────────

    private void CheckCollisions()
    {
        _wasGrounded = _grounded;
        _grounded = Physics2D.OverlapBox(
            (Vector2)transform.position + groundCheckOffset,
            groundCheckSize, 0f, groundLayer);

        _onWall = false;
        if (wallMechanicsEnabled)
        {
            bool r = Physics2D.OverlapBox((Vector2)transform.position + wallCheckOffR, wallCheckSize, 0f, groundLayer);
            bool l = Physics2D.OverlapBox((Vector2)transform.position + wallCheckOffL, wallCheckSize, 0f, groundLayer);
            if (r) { _onWall = true; _wallDir = 1f; }
            else if (l) { _onWall = true; _wallDir = -1f; }
        }

        if (!_grounded && _wasGrounded && ledgeForgiveness && Mathf.Abs(_vel.x) < 0.1f)
        {
            bool lr = Physics2D.OverlapBox((Vector2)transform.position + groundCheckOffset + new Vector2(ledgeForgiveRange, 0f), groundCheckSize, 0f, groundLayer);
            bool ll = Physics2D.OverlapBox((Vector2)transform.position + groundCheckOffset - new Vector2(ledgeForgiveRange, 0f), groundCheckSize, 0f, groundLayer);
            if (lr || ll) _grounded = true;
        }

        if (!_wasGrounded && _grounded)
        {
            _isJumping = false;
            _jumpCut = false;
            _airJumpsLeft = airJumps;
            _dashesLeft = 1;

            if (_vel.y < 0f) _vel.y = 0f;
            _subpixelAccum.y = 0f;

            if (squashStretch)
                _targetScale = new Vector3(
                    _baseScale.x * (1f + squashIntensity * 1.4f),
                    _baseScale.y * (1f - squashIntensity * 1.4f),
                    _baseScale.z);
        }
    }

    // ── 2. TIMERS ──────────────────────────────────────────────────

    private void TickTimers()
    {
        float dt = Time.fixedDeltaTime;
        if (_wasGrounded && !_grounded && !_isJumping) _coyoteTimer = coyoteTime;
        else _coyoteTimer -= dt;
        _jumpBufferTimer -= dt;
        _wallJumpTimer -= dt;
        _dashCooldownTimer -= dt;
    }

    // ── 3. GRAVITÉ ─────────────────────────────────────────────────

    private void HandleGravity()
    {
        if (_grounded && !_isJumping)
        {
            _vel.y = Mathf.MoveTowards(_vel.y, -1f, gravity * Time.fixedDeltaTime);
            return;
        }

        _atApex = _isJumping && Mathf.Abs(_vel.y) < apexThreshold;

        float g = gravity;
        if (_atApex) g *= apexGravityMult;
        else if (_jumpCut && _vel.y > 0f) g *= jumpCutMult;
        else if (_vel.y > 0f) g *= riseGravityMult;
        else g *= fallGravityMult;

        if (_wallSliding) g *= wallSlideGrav;

        _vel.y -= g * Time.fixedDeltaTime;
        _vel.y = Mathf.Max(_vel.y, -maxFallSpeed);
    }

    // ── 4. HORIZONTAL ──────────────────────────────────────────────

    private void HandleHorizontal()
    {
        if (_wallJumpTimer > 0f) return;

        float target = _inputX * maxSpeed;
        bool hasInput = Mathf.Abs(_inputX) > 0.01f;
        bool turning = hasInput
                       && Mathf.Sign(_inputX) != Mathf.Sign(_vel.x)
                       && Mathf.Abs(_vel.x) > 0.1f;

        float speedNorm = Mathf.Clamp01(Mathf.Abs(_vel.x) / Mathf.Max(runTopSpeed, 0.01f));
        float rate;

        if (hasInput)
        {
            float baseAccel = _grounded ? groundAccel : airAccel;
            float curveMult = _grounded
                ? groundAccelCurve.Evaluate(speedNorm)
                : airAccelCurve.Evaluate(speedNorm);
            rate = baseAccel * curveMult;
            if (turning) rate *= turnBoost;
        }
        else
        {
            // airDecel très faible → momentum conservé longtemps dans l'air
            rate = (_grounded ? groundDecel : airDecel) * decelCurve.Evaluate(speedNorm);
        }

        _vel.x = Mathf.MoveTowards(_vel.x, target, rate * Time.fixedDeltaTime);

        if (!hasInput && Mathf.Abs(_vel.x) < VEL_DEADZONE) _vel.x = 0f;

        // Pas de clamp strict dans l'air : le momentum peut dépasser maxSpeed
        // légèrement si on saute en sprint (comme Minecraft)
        float cap = _grounded ? runTopSpeed : runTopSpeed * 1.15f;
        _vel.x = Mathf.Clamp(_vel.x, -cap, cap);
    }

    // ── 5. SAUT ────────────────────────────────────────────────────

    private void HandleJump()
    {
        bool bufferActive = _jumpBufferTimer > 0f;
        bool canGround = bufferActive && (_grounded || _coyoteTimer > 0f);
        bool canWall = bufferActive && _onWall && !_grounded && wallMechanicsEnabled;
        bool canAir = _jumpPressedThisFrame && !_grounded && _airJumpsLeft > 0 && doubleJumpEnabled;

        if (canGround) DoJump(jumpHeight);
        else if (canWall) DoWallJump();
        else if (canAir) { _airJumpsLeft--; DoJump(jumpHeight * airJumpMult); }
    }

    private bool TryCornerCorrect(float jumpVel)
    {
        if (!cornerCorrection || jumpVel <= 0f) return false;
        float step = (colliderHalfW * 2f) / (cornerCorrRays - 1);
        Vector2 orig = (Vector2)transform.position + new Vector2(-colliderHalfW, colliderHalfH);
        for (int i = 0; i < cornerCorrRays; i++)
        {
            Vector2 ro = orig + new Vector2(step * i, 0f);
            if (!Physics2D.Raycast(ro, Vector2.up, 0.15f, groundLayer)) continue;
            for (float offset = pixelSize; offset <= cornerCorrDist; offset += pixelSize)
            {
                bool cR = !Physics2D.OverlapBox((Vector2)transform.position + new Vector2(offset, 0f) + groundCheckOffset, groundCheckSize, 0f, groundLayer)
                       && !Physics2D.OverlapBox((Vector2)transform.position + new Vector2(offset, colliderHalfH), new Vector2(colliderHalfW * 2f, 0.04f), 0f, groundLayer);
                if (cR) { transform.position += new Vector3(offset, 0f); return true; }
                bool cL = !Physics2D.OverlapBox((Vector2)transform.position + new Vector2(-offset, 0f) + groundCheckOffset, groundCheckSize, 0f, groundLayer)
                       && !Physics2D.OverlapBox((Vector2)transform.position + new Vector2(-offset, colliderHalfH), new Vector2(colliderHalfW * 2f, 0.04f), 0f, groundLayer);
                if (cL) { transform.position += new Vector3(-offset, 0f); return true; }
            }
        }
        return false;
    }

    private void DoJump(float height)
    {
        _jumpBufferTimer = 0f;
        _coyoteTimer = 0f;
        _isJumping = true;
        _jumpCut = false;
        _subpixelAccum.y = 0f;
        // Note : _vel.x N'EST PAS réinitialisé → momentum horizontal conservé
        float v = Mathf.Sqrt(2f * gravity * height);
        TryCornerCorrect(v);
        _vel.y = v;
        if (squashStretch)
            _targetScale = new Vector3(
                _baseScale.x * (1f - squashIntensity * 0.8f),
                _baseScale.y * (1f + squashIntensity),
                _baseScale.z);
    }

    private void DoWallJump()
    {
        _jumpBufferTimer = 0f;
        _isJumping = true;
        _jumpCut = false;
        _wallSliding = false;
        _vel.y = Mathf.Sqrt(2f * gravity * jumpHeight);
        _vel.x = -_wallDir * wallJumpForceX;
        _wallJumpTimer = wallJumpLock;
        if (squashStretch)
            _targetScale = new Vector3(
                _baseScale.x * (1f - squashIntensity),
                _baseScale.y * (1f + squashIntensity),
                _baseScale.z);
    }

    // ── 6. WALL SLIDE ──────────────────────────────────────────────

    private void HandleWallSlide()
    {
        _wallSliding = wallMechanicsEnabled && _onWall && !_grounded && _vel.y < 0f && _inputX * _wallDir > 0f;
    }

    // ── 7. DASH ────────────────────────────────────────────────────

    private void TryDash()
    {
        if (!dashEnabled || !_dashPressedThisFrame || _dashesLeft <= 0 || _dashCooldownTimer > 0f) return;
        _dashesLeft--;
        _isDashing = true;
        _dashTimer = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _jumpCut = false;
        _dashDirX = _inputX != 0f ? Mathf.Sign(_inputX) : (transform.localScale.x >= 0f ? 1f : -1f);
        _vel = new Vector2(_dashDirX * dashSpeed, 0f);
    }

    private void TickDashMotion()
    {
        _dashTimer -= Time.fixedDeltaTime;
        float t = 1f - (_dashTimer / dashDuration);
        _vel = new Vector2(_dashDirX * Mathf.Lerp(dashSpeed, dashSpeed * 0.75f, t * t), 0f);
        if (_dashTimer <= 0f)
        {
            _isDashing = false;
            _vel.y = 0f;
            _vel.x = _dashDirX * maxSpeed * dashExitMomentum;
        }
    }

    // ── 8. APPLY VELOCITY ──────────────────────────────────────────

    private void ApplyVelocity()
    {
        if (!subpixelMovement) { _rb.linearVelocity = _vel; return; }

        float dt = Time.fixedDeltaTime;
        if (Mathf.Abs(_vel.x) < VEL_DEADZONE) _subpixelAccum.x = 0f;
        if (Mathf.Abs(_vel.y) < VEL_DEADZONE) _subpixelAccum.y = 0f;

        _subpixelAccum += _vel * dt;

        float px = Mathf.Floor(Mathf.Abs(_subpixelAccum.x) / pixelSize) * pixelSize * Mathf.Sign(_subpixelAccum.x);
        float py = Mathf.Floor(Mathf.Abs(_subpixelAccum.y) / pixelSize) * pixelSize * Mathf.Sign(_subpixelAccum.y);
        _subpixelAccum.x -= px;
        _subpixelAccum.y -= py;

        _rb.linearVelocity = dt > 0f ? new Vector2(px / dt, py / dt) : Vector2.zero;
    }

    // ── 9. SQUASH & STRETCH ────────────────────────────────────────

    private void UpdateSquashStretch()
    {
        if (!_grounded && !_isDashing)
        {
            float norm = Mathf.Clamp(_vel.y / Mathf.Sqrt(2f * gravity * jumpHeight), -1f, 1f);
            float sY = 1f + norm * squashIntensity;
            _targetScale = new Vector3(_baseScale.x / Mathf.Max(sY, 0.01f), _baseScale.y * sY, _baseScale.z);
        }
        else if (_grounded && Mathf.Abs(_vel.x) > 0.5f)
        {
            _targetScale = new Vector3(
                _baseScale.x * (1f + squashIntensity * 0.10f),
                _baseScale.y * (1f - squashIntensity * 0.06f),
                _baseScale.z);
        }
        else if (_grounded)
        {
            _targetScale = _baseScale;
        }
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * 18f);
    }

    // ── 10. DEBUG ──────────────────────────────────────────────────

    private void DebugLog()
    {
        if (!debugPosition) return;
        _debugTimer -= Time.deltaTime;
        if (_debugTimer > 0f) return;
        _debugTimer = DEBUG_INTERVAL;

        Vector3 pos = transform.position;
        string log = $"[Player] Pos=({pos.x:F3}, {pos.y:F3}) | Pieds={pos.y - colliderHalfH:F3} | Tête={pos.y + colliderHalfH:F3}";
        if (debugVerbose)
            log += $" | Vel=({_vel.x:F2}, {_vel.y:F2}) | Grounded={_grounded} | Jumping={_isJumping} | Apex={_atApex} | WallSlide={_wallSliding} | Dashing={_isDashing}";
        Debug.Log(log);
    }

    // ── API PUBLIQUE ───────────────────────────────────────────────

    public void UnlockWall() => wallMechanicsEnabled = true;
    public void UnlockDoubleJump() => doubleJumpEnabled = true;
    public void UnlockDash() => dashEnabled = true;
    public void LockAll() => wallMechanicsEnabled = doubleJumpEnabled = dashEnabled = false;
    public void UnlockAll() => wallMechanicsEnabled = doubleJumpEnabled = dashEnabled = true;

    // ── GIZMOS ─────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + (Vector3)groundCheckOffset, groundCheckSize);
        if (wallMechanicsEnabled)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + (Vector3)wallCheckOffR, wallCheckSize);
            Gizmos.DrawWireCube(transform.position + (Vector3)wallCheckOffL, wallCheckSize);
        }
        if (cornerCorrection)
        {
            Gizmos.color = Color.yellow;
            float step = (colliderHalfW * 2f) / (cornerCorrRays - 1);
            Vector3 start = transform.position + new Vector3(-colliderHalfW, colliderHalfH, 0f);
            for (int i = 0; i < cornerCorrRays; i++)
            {
                Vector3 o = start + new Vector3(step * i, 0f, 0f);
                Gizmos.DrawLine(o, o + Vector3.up * 0.15f);
            }
        }
        if (ledgeForgiveness)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + (Vector3)(groundCheckOffset + new Vector2(ledgeForgiveRange, 0f)), groundCheckSize);
            Gizmos.DrawWireCube(transform.position + (Vector3)(groundCheckOffset - new Vector2(ledgeForgiveRange, 0f)), groundCheckSize);
        }
    }
}