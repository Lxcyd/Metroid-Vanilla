 using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //   CAPACITES
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  CAPACITES DEBLOQUEES  ━━━━━━━━━━")]
    public bool wallMechanicsEnabled = false;
    public bool doubleJumpEnabled    = false;
    public bool dashEnabled          = false;
    public bool longJumpEnabled      = true;

    // ══════════════════════════════════════════════════════════════
    //   HORIZONTAL
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  HORIZONTAL  ━━━━━━━━━━━━━━━━━━━━")]
    public float maxSpeed    = 9f;
    public float runTopSpeed = 9f;

    [Tooltip("Acceleration sol")]
    public float groundAccel = 50f;

    [Tooltip("Deceleration sol")]
    public float groundDecel = 20f;

    [Tooltip("Acceleration air")]
    public float airAccel = 75f;

    [Tooltip("Deceleration air. TRES faible = momentum Minecraft")]
    public float airDecel = 30f;

    [Tooltip("Drag exponentiel en l air quand on depasse maxSpeed (% vitesse perdue / s). 0=infini, 0.12=doux, 1=brutal)")]
    [Range(0f, 1f)]
    public float airOverspeedDrag = 0.12f;

    [Range(1f, 4f)]
    public float turnBoost = 2f;

    private const float VEL_DEADZONE = 0.05f;

    [Header("Dynamic Acceleration Curves")]
    public AnimationCurve groundAccelCurve = AnimationCurve.EaseInOut(0f, 1.2f, 1f, 0.8f);
    public AnimationCurve airAccelCurve    = AnimationCurve.EaseInOut(0f, 1.0f, 1f, 0.7f);
    public AnimationCurve decelCurve       = AnimationCurve.EaseInOut(0f, 0.8f, 1f, 1.2f);

    // ══════════════════════════════════════════════════════════════
    //   SAUT ET GRAVITE
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  SAUT ET GRAVITE  ━━━━━━━━━━━━━━━")]
    public float jumpHeight = 3.5f;
    public float gravity    = 30f;

    [Header("Variable Gravity Curves")]
    [Tooltip("Multiplicateur de gravite en montee normale.\nX = vel.y normalisee (0=apex, 1=vitesse de saut max)\nY = multiplicateur")]
    public AnimationCurve riseGravityCurve = new AnimationCurve(
        new Keyframe(0f, 0.80f, 0f, 0f),
        new Keyframe(1f, 0.55f, 0f, 0f));

    [Tooltip("Multiplicateur de gravite en descente.\nX = |vel.y| normalisee (0=debut chute, 1=vitesse chute max)\nY = multiplicateur")]
    public AnimationCurve fallGravityCurve = new AnimationCurve(
        new Keyframe(0f, 3.0f, 0f, 0f),
        new Keyframe(1f, 2.5f, 0f, 0f));

    [Tooltip("Multiplicateur de gravite apres relachement du bouton saut (variable jump height).\nX = vel.y normalisee (0=apex, 1=vitesse de saut max)\nY = multiplicateur")]
    public AnimationCurve jumpCutCurve = new AnimationCurve(
        new Keyframe(0f, 4.0f, 0f, 0f),
        new Keyframe(1f, 6.0f, 0f, 0f));

    public float apexThreshold = 0.8f;
    public float maxFallSpeed  = 22f;

    [Range(0, 3)]     public int   airJumps    = 0;
    [Range(0.5f, 1f)] public float airJumpMult = 0.9f;

    // ══════════════════════════════════════════════════════════════
    //   HEAD HITTER (ceiling coyote + accumulation additive)
    //
    //   Fonctionnement inspiré de Minecraft parkour :
    //   - Chaque bump plafond : vel.y = 0 + vel.x += headBumpBonusSpeed
    //   - Un "ceiling coyote timer" permet de re-sauter immédiatement
    //     après le bump → spammer jump = accumulation fluide et responsive
    //   - La vitesse est protégée contre le drag air via headBumpCapTimer
    //
    //   Anti-spam : un cooldown (_ceilingHitCooldown) bloque la
    //   re-detection pendant headBumpCoyoteTime apres chaque bump.
    //   Sans ça, spammer jump sous un plafond bas empêche de tomber.
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  HEAD HITTER  ━━━━━━━━━━━━━━━━━━━━━")]
    public bool headHitterEnabled = true;

    [Tooltip("Vitesse AJOUTEE a chaque bump (u/s). Ex: 2.5 = +2.5 u/s par bump")]
    public float headBumpBonusSpeed = 5.0f;

    [Tooltip("Vitesse X minimale pour déclencher le bonus")]
    public float headBumpMinSpeed = 1.0f;

    [Tooltip("Vitesse X maximale atteignable via head-bump")]
    public float headBumpMaxSpeed = 22f;

    [Tooltip("Fenetre (s) apres un bump pendant laquelle on peut re-sauter immédiatement (ceiling coyote)")]
    [Range(0.05f, 0.2f)]
    public float headBumpCoyoteTime = 0.12f;

    [Tooltip("Secondes pendant lesquelles la vitesse bump resist a la decel air")]
    public float headBumpCapDuration = 0.5f;

    [Tooltip("Distance max entre les pieds du joueur et le sol pour activer le head hitter.\nAu-dela, le plafond stoppe juste la montee sans bonus ni coyote.")]
    public float headBumpMaxGroundDistance = 0.5f;

    // ══════════════════════════════════════════════════════════════
    //   FEEL
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  FEEL ET RESPONSIVENESS  ━━━━━━━━")]
    [Range(0f, 0.2f)] public float jumpBufferTime = 0.08f;
    [Range(0f, 0.2f)] public float coyoteTime     = 0.08f;

    [Header("Corner Correction")]
    public bool cornerCorrection = true;
    [Range(0.05f, 0.6f)] public float cornerCorrDist = 0.25f;
    [Range(2, 8)]        public int   cornerCorrRays  = 4;

    [Header("Ledge Forgiveness")]
    public bool ledgeForgiveness = true;
    [Range(0.05f, 0.4f)] public float ledgeForgiveRange = 0.15f;

    [Header("Input Latency Reduction")]
    [Range(0f, 0.05f)] public float inputDebounce = 0.016f;

    [Header("Subpixel Movement")]
    public bool  subpixelMovement = true;
    public float pixelSize        = 0.0625f;

    [Header("Squash and Stretch")]
    public bool squashStretch = true;
    [Range(0f, 0.4f)] public float squashIntensity = 0.10f;

    // ══════════════════════════════════════════════════════════════
    //   LONG JUMP
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  LONG JUMP  ━━━━━━━━━━━━━━━━━━━━━")]
    [Range(0.05f, 0.35f)] public float longJumpWindow     = 0.20f;
    [Range(0.5f,  1.5f)]  public float longJumpHeightMult = 1.35f;
    public float longJumpSpeedX  = 21f;
    [Range(0f, 0.4f)]     public float longJumpLock       = 0.20f;

    // ══════════════════════════════════════════════════════════════
    //   WALL
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  WALL JUMP / WALL SLIDE  ━━━━━━━━")]
    public float wallJumpForceX = 9.5f;
    [Range(0f, 0.5f)] public float wallJumpLock  = 0.15f;
    [Range(0f, 0.8f)] public float wallSlideGrav = 0.12f;

    [Tooltip("Fenetre (s) pendant laquelle un jump presse avant de toucher le mur declenchera un wall jump")]
    [Range(0f, 0.25f)] public float wallJumpBufferTime = 0.12f;

    [Tooltip("Multiplicateur de hauteur du wall jump quand le joueur vient de dasher dans le mur.\n1 = hauteur normale, 2 = double hauteur.")]
    [Range(1f, 4f)] public float wallDashJumpMult = 2.0f;

    [Tooltip("Fenetre (s) apres un dash-into-wall pendant laquelle le bonus de wall jump est actif.")]
    [Range(0f, 0.5f)] public float wallDashWindow = 0.25f;

    // ══════════════════════════════════════════════════════════════
    //   DASH
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  DASH  ━━━━━━━━━━━━━━━━━━━━━━━━━━")]
    public float dashSpeed    = 17f;
    [Range(0.05f, 0.3f)] public float dashDuration     = 0.09f;
    [Range(0.1f, 2f)]    public float dashCooldown     = 0.6f;
    [Range(0f, 2f)]      public float dashExitMomentum = 1.0f;

    // ══════════════════════════════════════════════════════════════
    //   COLLISION DETECTION
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  COLLISION DETECTION  ━━━━━━━━━━━")]
    public LayerMask groundLayer;
    public Vector2 groundCheckOffset = new Vector2(0f, -0.34f);
    public Vector2 groundCheckSize   = new Vector2(0.18f, 0.05f);
    public Vector2 wallCheckOffR     = new Vector2(0.13f, 0f);
    public Vector2 wallCheckOffL     = new Vector2(-0.13f, 0f);
    public Vector2 wallCheckSize     = new Vector2(0.05f, 0.28f);
    public float colliderHalfH = 0.33638f;
    public float colliderHalfW = 0.11302f;

    // ══════════════════════════════════════════════════════════════
    //   ETAT INTERNE - INPUT
    // ══════════════════════════════════════════════════════════════

    private PlayerInputActions _inputActions;

    private float _inputX;
    private bool  _jumpPressedThisFrame;
    private bool  _jumpReleasedThisFrame;
    private bool  _dashPressedThisFrame;
    private float _debounceTimer;
    private float _jumpDebounce;
    private float _dashDebounce;

    // ══════════════════════════════════════════════════════════════
    //   ETAT INTERNE - PHYSIQUE
    // ══════════════════════════════════════════════════════════════

    private Rigidbody2D _rb;
    private Vector2 _vel;

    // Vélocité Y calculée par nous au frame précédent, avant que
    // Unity la modifie via résolution de collision.
    // Permet de détecter un ceiling hit même quand _vel.y a déjà
    // été remis à 0 par le moteur physique.
    private float _prevVelY;

    private bool  _grounded, _wasGrounded;
    private bool  _onWall;
    private float _wallDir;

    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _wallJumpTimer;
    private float _dashCooldownTimer;

    private bool  _isJumping;
    private bool  _jumpCut;
    private int   _airJumpsLeft;
    private bool  _atApex;
    private bool  _wallSliding;

    private bool  _isDashing;
    private float _dashTimer;
    private int   _dashesLeft;
    private float _dashDirX;
    private float _lastDirX = 1f;

    // Head hitter
    private float _headBumpSpeedCap  = 0f;
    private float _headBumpCapTimer  = 0f;
    // Cooldown anti-spam : bloque la re-detection plafond pendant
    // headBumpCoyoteTime apres chaque bump. Empeche de rester colle
    // en spammant jump sous un plafond bas.
    private float _ceilingHitCooldown = 0f;

    private float _jumpPressTime  = -999f;
    private float _dashPressTime  = -999f;
    private bool  _longJumpQueued = false;

    // Wall jump buffer : fenetre avant de toucher le mur
    private float _wallJumpBufferTimer = 0f;

    // Wall dash bonus : fenetre apres un dash-into-wall pour wall jump booste
    private float _wallDashTimer = 0f;

    // Ceiling coyote : fenetre apres un bump plafond pour re-sauter
    private float _ceilingCoyoteTimer = 0f;

    private Vector2 _subpixelAccum;
    private Vector3 _baseScale;
    private Vector3 _targetScale;

    // ══════════════════════════════════════════════════════════════
    //   INITIALISATION
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale           = 0f;
        _rb.interpolation          = RigidbodyInterpolation2D.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.constraints            = RigidbodyConstraints2D.FreezeRotation;

        _baseScale   = transform.localScale;
        _targetScale = _baseScale;

        _inputActions = new PlayerInputActions();
        _inputActions.Player.Jump.started  += OnJumpStarted;
        _inputActions.Player.Jump.canceled += OnJumpCanceled;
        _inputActions.Player.Dash.started  += OnDashStarted;
    }

    private void OnEnable()  => _inputActions.Enable();
    private void OnDisable() => _inputActions.Disable();

    private void OnDestroy()
    {
        _inputActions.Player.Jump.started  -= OnJumpStarted;
        _inputActions.Player.Jump.canceled -= OnJumpCanceled;
        _inputActions.Player.Dash.started  -= OnDashStarted;
        _inputActions.Dispose();
    }

    private void Start()
    {
        _airJumpsLeft = airJumps;
        _dashesLeft   = 1;
    }

    // ══════════════════════════════════════════════════════════════
    //   CALLBACKS INPUT
    // ══════════════════════════════════════════════════════════════

    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        if (_jumpDebounce > 0f) return;
        _jumpPressedThisFrame  = true;
        _jumpDebounce          = inputDebounce;
        _jumpBufferTimer       = jumpBufferTime;
        _jumpPressTime         = Time.realtimeSinceStartup;

        _wallJumpBufferTimer = wallJumpBufferTime;

        if (longJumpEnabled && (_grounded || _coyoteTimer > 0f)
            && Time.realtimeSinceStartup - _dashPressTime <= longJumpWindow)
            _longJumpQueued = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        _jumpReleasedThisFrame = true;
    }

    private void OnDashStarted(InputAction.CallbackContext ctx)
    {
        if (_dashDebounce > 0f) return;
        _dashPressedThisFrame = true;
        _dashDebounce         = inputDebounce;
        _dashPressTime        = Time.realtimeSinceStartup;

        if (longJumpEnabled && (_grounded || _coyoteTimer > 0f)
            && Time.realtimeSinceStartup - _jumpPressTime <= longJumpWindow)
            _longJumpQueued = true;
    }

    // ══════════════════════════════════════════════════════════════
    //   LOOP
    // ══════════════════════════════════════════════════════════════

    private void Update()
    {
        _debounceTimer -= Time.deltaTime;
        _jumpDebounce  -= Time.deltaTime;
        _dashDebounce  -= Time.deltaTime;
        _inputX = _inputActions.Player.Move.ReadValue<Vector2>().x;
        if (Mathf.Abs(_inputX) > 0.01f) _lastDirX = Mathf.Sign(_inputX);

        if (_jumpReleasedThisFrame && _isJumping && _vel.y > 0f)
            _jumpCut = true;

        if (squashStretch) UpdateSquashStretch();
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

        _jumpPressedThisFrame  = false;
        _jumpReleasedThisFrame = false;
        _dashPressedThisFrame  = false;
    }

    // ── 1. COLLISIONS ─────────────────────────────────────────────

    private void CheckCollisions()
    {
        _wasGrounded = _grounded;
        _grounded = Physics2D.OverlapBox(
            (Vector2)transform.position + groundCheckOffset,
            groundCheckSize, 0f, groundLayer);

        bool wasOnWall = _onWall;
        _onWall = false;
        if (wallMechanicsEnabled)
        {
            bool r = Physics2D.OverlapBox((Vector2)transform.position + wallCheckOffR, wallCheckSize, 0f, groundLayer);
            bool l = Physics2D.OverlapBox((Vector2)transform.position + wallCheckOffL, wallCheckSize, 0f, groundLayer);
            if (r)      { _onWall = true; _wallDir =  1f; }
            else if (l) { _onWall = true; _wallDir = -1f; }

            if (_onWall && !wasOnWall && !_grounded)
            {
                _dashesLeft   = 1;
                _airJumpsLeft = airJumps;

                // Dash-into-wall : armer le bonus de wall jump
                if (_isDashing)
                    _wallDashTimer = wallDashWindow;
            }
        }

        // Ledge forgiveness
        if (!_grounded && _wasGrounded && ledgeForgiveness && Mathf.Abs(_vel.x) < 0.1f)
        {
            bool lr = Physics2D.OverlapBox((Vector2)transform.position + groundCheckOffset + new Vector2( ledgeForgiveRange, 0f), groundCheckSize, 0f, groundLayer);
            bool ll = Physics2D.OverlapBox((Vector2)transform.position + groundCheckOffset - new Vector2( ledgeForgiveRange, 0f), groundCheckSize, 0f, groundLayer);
            if (lr || ll) _grounded = true;
        }

        // Atterrissage
        if (!_wasGrounded && _grounded)
        {
            _isJumping    = false;
            _jumpCut      = false;
            _airJumpsLeft = airJumps;
            _dashesLeft   = 1;

            if (_vel.y < 0f) _vel.y = 0f;
            _subpixelAccum.y = 0f;

            if (squashStretch)
                _targetScale = new Vector3(
                    _baseScale.x * (1f + squashIntensity * 1.4f),
                    _baseScale.y * (1f - squashIntensity * 1.4f),
                    _baseScale.z);
        }

        // ── HEAD HITTER ──────────────────────────────────────────
        //
        // _ceilingHitCooldown empeche la re-detection pendant la fenetre
        // coyote qui suit le bump. Sans ce cooldown, spammer jump sous un
        // plafond bas re-arme le coyote timer a chaque frame et le joueur
        // ne tombe jamais.
        if (headHitterEnabled && _prevVelY > 0f && _ceilingHitCooldown <= 0f)
        {
            bool hitCeiling = Physics2D.OverlapBox(
                (Vector2)transform.position + new Vector2(0f, colliderHalfH + 0.05f),
                new Vector2(colliderHalfW * 1.6f, 0.05f),
                0f, groundLayer);

            if (hitCeiling)
            {
                _vel.y           = 0f;
                _jumpCut         = true;
                _subpixelAccum.y = 0f;
                _isJumping       = false;

                // Verifier que le sol est assez proche pour activer le head hitter.
                // On raycast vers le bas depuis les pieds du joueur.
                // Si le sol est trop loin (plafond en hauteur, pas un tunnel),
                // on annule juste la vitesse Y sans bonus ni ceiling coyote.
                RaycastHit2D groundBelow = Physics2D.Raycast(
                    (Vector2)transform.position + groundCheckOffset,
                    Vector2.down,
                    headBumpMaxGroundDistance,
                    groundLayer);

                if (groundBelow.collider == null) return;

                // Armer le ceiling coyote et le cooldown anti-spam.
                // Le cooldown dure autant que la fenetre coyote : le joueur
                // peut re-sauter une fois dans cette fenetre, mais un
                // nouveau bump ne peut pas re-armer le timer tant qu'il
                // n'est pas expire.
                _ceilingCoyoteTimer = headBumpCoyoteTime;
                _ceilingHitCooldown = headBumpCoyoteTime;

                // Boost additif
                if (Mathf.Abs(_vel.x) >= headBumpMinSpeed)
                {
                    float dir      = Mathf.Sign(_vel.x);
                    float newSpeed = Mathf.Min(Mathf.Abs(_vel.x) + headBumpBonusSpeed, headBumpMaxSpeed);
                    _vel.x            = dir * newSpeed;
                    _headBumpSpeedCap = newSpeed;
                    _headBumpCapTimer = headBumpCapDuration;
                }
            }
        }
    }

    // ── 2. TIMERS ──────────────────────────────────────────────────

    private void TickTimers()
    {
        float dt = Time.fixedDeltaTime;
        if (_wasGrounded && !_grounded && !_isJumping) _coyoteTimer = coyoteTime;
        else _coyoteTimer -= dt;
        _jumpBufferTimer     -= dt;
        _wallJumpTimer       -= dt;
        _wallJumpBufferTimer -= dt;
        _ceilingCoyoteTimer  -= dt;
        _ceilingHitCooldown  -= dt;
        _wallDashTimer       -= dt;
        _dashCooldownTimer   -= dt;
        if (_headBumpCapTimer > 0f) { _headBumpCapTimer -= dt; if (_headBumpCapTimer <= 0f) _headBumpSpeedCap = 0f; }
    }

    // ── 3. GRAVITE ─────────────────────────────────────────────────
    //
    //   riseGravityCurve  — montee normale
    //     X : vel.y / peakVel    (0 = apex, 1 = juste apres le saut)
    //     Y : multiplicateur
    //
    //   jumpCutCurve      — montee apres relachement du bouton
    //     X : meme normalisation
    //     Y : multiplicateur (valeur haute = retombee rapide)
    //
    //   fallGravityCurve  — descente
    //     X : |vel.y| / maxFallSpeed    (0 = debut chute, 1 = vitesse max)
    //     Y : multiplicateur

    private void HandleGravity()
    {
        if (_grounded && !_isJumping)
        {
            _vel.y = Mathf.MoveTowards(_vel.y, -1f, gravity * Time.fixedDeltaTime);
            return;
        }

        float peakVel = Mathf.Sqrt(2f * gravity * jumpHeight);
        float g;

        if (_jumpCut && _vel.y > 0f)
        {
            float t = Mathf.Clamp01(_vel.y / Mathf.Max(peakVel, 0.01f));
            g = gravity * jumpCutCurve.Evaluate(t);
        }
        else if (_vel.y > 0f)
        {
            float t = Mathf.Clamp01(_vel.y / Mathf.Max(peakVel, 0.01f));
            g = gravity * riseGravityCurve.Evaluate(t);
        }
        else
        {
            float t = Mathf.Clamp01(Mathf.Abs(_vel.y) / Mathf.Max(maxFallSpeed, 0.01f));
            g = gravity * fallGravityCurve.Evaluate(t);
        }

        _atApex = _isJumping && Mathf.Abs(_vel.y) < apexThreshold;

        if (_wallSliding) g *= wallSlideGrav;

        _vel.y -= g * Time.fixedDeltaTime;
        _vel.y  = Mathf.Max(_vel.y, -maxFallSpeed);
    }

    // ── 4. HORIZONTAL ──────────────────────────────────────────────

    private void HandleHorizontal()
    {
        if (_wallJumpTimer > 0f) return;

        bool  hasInput = Mathf.Abs(_inputX) > 0.01f;
        float target   = _inputX * maxSpeed;

        if (_headBumpCapTimer > 0f && hasInput && Mathf.Sign(_inputX) == Mathf.Sign(_vel.x))
            target = Mathf.Sign(_inputX) * Mathf.Max(Mathf.Abs(target), _headBumpSpeedCap);

        bool  turning  = hasInput
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
            rate = (_grounded ? groundDecel : airDecel) * decelCurve.Evaluate(speedNorm);
        }

        _vel.x = Mathf.MoveTowards(_vel.x, target, rate * Time.fixedDeltaTime);

        if (!hasInput && !_grounded && Mathf.Abs(_vel.x) > maxSpeed)
        {
            float drag = 1f - airOverspeedDrag * Time.fixedDeltaTime;
            _vel.x *= drag;
            if (Mathf.Abs(_vel.x) < maxSpeed)
                _vel.x = Mathf.Sign(_vel.x) * maxSpeed;
        }

        if (!hasInput && Mathf.Abs(_vel.x) < VEL_DEADZONE) _vel.x = 0f;

        float baseCap = _grounded ? runTopSpeed : runTopSpeed * 2.2f;
        float cap     = _headBumpCapTimer > 0f
                        ? Mathf.Max(baseCap, _headBumpSpeedCap)
                        : baseCap;
        _vel.x = Mathf.Clamp(_vel.x, -cap, cap);
    }

    // ── 5. SAUT ────────────────────────────────────────────────────

    private void HandleJump()
    {
        bool bufferActive = _jumpBufferTimer > 0f;

        bool pressingIntoWall = _inputX * _wallDir > 0f;
        bool wallBufferActive = _wallJumpBufferTimer > 0f;
        bool canWall          = (bufferActive || wallBufferActive) && _onWall
                             && wallMechanicsEnabled && pressingIntoWall;

        bool canGround = bufferActive && (_grounded || _coyoteTimer > 0f || _ceilingCoyoteTimer > 0f);
        bool canAir    = _jumpPressedThisFrame && !_grounded && _airJumpsLeft > 0 && doubleJumpEnabled;

        if (_longJumpQueued && canGround)
        {
            _longJumpQueued       = false;
            _dashPressedThisFrame = false;
            DoLongJump();
        }
        else if (canWall)   DoWallJump();
        else if (canGround) DoJump(jumpHeight);
        else if (canAir)    { _airJumpsLeft--; DoJump(jumpHeight * airJumpMult); }

        if (!canGround) _longJumpQueued = false;
    }

    private bool TryCornerCorrect(float jumpVel)
    {
        if (!cornerCorrection || jumpVel <= 0f) return false;
        float   step = (colliderHalfW * 2f) / (cornerCorrRays - 1);
        Vector2 orig = (Vector2)transform.position + new Vector2(-colliderHalfW, colliderHalfH);
        for (int i = 0; i < cornerCorrRays; i++)
        {
            Vector2 ro = orig + new Vector2(step * i, 0f);
            if (!Physics2D.Raycast(ro, Vector2.up, 0.08f, groundLayer)) continue;
            for (float offset = pixelSize; offset <= cornerCorrDist; offset += pixelSize)
            {
                bool cR = !Physics2D.OverlapBox((Vector2)transform.position + new Vector2( offset, 0f) + groundCheckOffset, groundCheckSize, 0f, groundLayer)
                       && !Physics2D.OverlapBox((Vector2)transform.position + new Vector2( offset, colliderHalfH), new Vector2(colliderHalfW * 2f, 0.04f), 0f, groundLayer);
                if (cR) { transform.position += new Vector3( offset, 0f); return true; }

                bool cL = !Physics2D.OverlapBox((Vector2)transform.position + new Vector2(-offset, 0f) + groundCheckOffset, groundCheckSize, 0f, groundLayer)
                       && !Physics2D.OverlapBox((Vector2)transform.position + new Vector2(-offset, colliderHalfH), new Vector2(colliderHalfW * 2f, 0.04f), 0f, groundLayer);
                if (cL) { transform.position += new Vector3(-offset, 0f); return true; }
            }
        }
        return false;
    }

    private void DoJump(float height)
    {
        _jumpBufferTimer    = 0f;
        _coyoteTimer        = 0f;
        _ceilingCoyoteTimer = 0f;
        _isJumping          = true;
        _jumpCut            = false;
        _grounded           = false;
        _subpixelAccum.y    = 0f;

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
        _jumpBufferTimer     = 0f;
        _wallJumpBufferTimer = 0f;
        _isJumping           = true;
        _jumpCut             = false;
        _wallSliding         = false;
        _grounded            = false;

        float heightMult = _wallDashTimer > 0f ? wallDashJumpMult : 1f;
        _vel.y         = Mathf.Sqrt(2f * gravity * jumpHeight * heightMult);
        _vel.x         = -_wallDir * wallJumpForceX;
        _wallJumpTimer = wallJumpLock;
        _wallDashTimer = 0f;

        if (squashStretch)
            _targetScale = new Vector3(
                _baseScale.x * (1f - squashIntensity),
                _baseScale.y * (1f + squashIntensity),
                _baseScale.z);
    }

    private void DoLongJump()
    {
        _jumpBufferTimer = 0f;
        _coyoteTimer     = 0f;
        _isJumping       = true;
        _jumpCut         = false;
        _grounded        = false;
        _subpixelAccum.y = 0f;

        float dir  = _inputX != 0f ? Mathf.Sign(_inputX) : _lastDirX;
        _vel.y     = Mathf.Sqrt(2f * gravity * jumpHeight * longJumpHeightMult);
        _vel.x     = dir * longJumpSpeedX;
        _wallJumpTimer = longJumpLock;

        if (squashStretch)
            _targetScale = new Vector3(
                _baseScale.x * (1f - squashIntensity * 1.2f),
                _baseScale.y * (1f + squashIntensity * 0.6f),
                _baseScale.z);
    }

    // ── 6. WALL SLIDE ──────────────────────────────────────────────

    private void HandleWallSlide()
    {
        _wallSliding = wallMechanicsEnabled && _onWall && !_grounded
                    && _vel.y < 0f && _inputX * _wallDir > 0f;
    }

    // ── 7. DASH ────────────────────────────────────────────────────

    private void TryDash()
    {
        if (!dashEnabled || !_dashPressedThisFrame || _dashesLeft <= 0 || _dashCooldownTimer > 0f) return;
        _dashesLeft--;
        _isDashing         = true;
        _dashTimer         = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _jumpCut           = false;
        _dashDirX          = _inputX != 0f ? Mathf.Sign(_inputX) : _lastDirX;
        _vel               = new Vector2(_dashDirX * dashSpeed, 0f);
    }

    private void TickDashMotion()
    {
        _dashTimer -= Time.fixedDeltaTime;
        float t = 1f - (_dashTimer / dashDuration);
        _vel = new Vector2(_dashDirX * Mathf.Lerp(dashSpeed, dashSpeed * 0.75f, t * t), 0f);

        if (_dashTimer <= 0f)
        {
            _isDashing = false;
            _vel.y     = 0f;

            if (_grounded)
            {
                _vel.x = _dashDirX * maxSpeed * dashExitMomentum;
            }
            else
            {
                float airExitSpeed = maxSpeed * dashExitMomentum;
                _vel.x            = _dashDirX * airExitSpeed;
                _headBumpSpeedCap = airExitSpeed;
                _headBumpCapTimer = headBumpCapDuration * 0.4f;
            }

            if (_grounded || _onWall)
                _dashesLeft = 1;
        }
    }

    // ── 8. APPLY VELOCITY ──────────────────────────────────────────

    private void ApplyVelocity()
    {
        _prevVelY = _vel.y;

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

    // ── 9. SQUASH ET STRETCH ───────────────────────────────────────

    private void UpdateSquashStretch()
    {
        if (!_grounded && !_isDashing)
        {
            float norm = Mathf.Clamp(_vel.y / Mathf.Sqrt(2f * gravity * jumpHeight), -1f, 1f);
            float sY   = 1f + norm * squashIntensity;
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

    // ── API PUBLIQUE ───────────────────────────────────────────────

    public void UnlockWall()       => wallMechanicsEnabled = true;
    public void UnlockDoubleJump() => doubleJumpEnabled    = true;
    public void UnlockDash()       => dashEnabled          = true;
    public void UnlockLongJump()   => longJumpEnabled      = true;
    public void LockAll()          => wallMechanicsEnabled = doubleJumpEnabled = dashEnabled = longJumpEnabled = false;
    public void UnlockAll()        => wallMechanicsEnabled = doubleJumpEnabled = dashEnabled = longJumpEnabled = true;

    // ── GIZMOS ─────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + (Vector3)groundCheckOffset, groundCheckSize);

        if (headHitterEnabled)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.1f);
            Gizmos.DrawWireCube(
                transform.position + new Vector3(0f, colliderHalfH + 0.05f, 0f),
                new Vector3(colliderHalfW * 1.6f, 0.05f, 0f));
        }
        if (wallMechanicsEnabled)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + (Vector3)wallCheckOffR, wallCheckSize);
            Gizmos.DrawWireCube(transform.position + (Vector3)wallCheckOffL, wallCheckSize);
        }
        if (cornerCorrection)
        {
            Gizmos.color = Color.yellow;
            float   step  = (colliderHalfW * 2f) / (cornerCorrRays - 1);
            Vector3 start = transform.position + new Vector3(-colliderHalfW, colliderHalfH, 0f);
            for (int i = 0; i < cornerCorrRays; i++)
            {
                Vector3 o = start + new Vector3(step * i, 0f, 0f);
                Gizmos.DrawLine(o, o + Vector3.up * 0.08f);
            }
        }
        if (ledgeForgiveness)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + (Vector3)(groundCheckOffset + new Vector2( ledgeForgiveRange, 0f)), groundCheckSize);
            Gizmos.DrawWireCube(transform.position + (Vector3)(groundCheckOffset - new Vector2( ledgeForgiveRange, 0f)), groundCheckSize);
        }
    }
}