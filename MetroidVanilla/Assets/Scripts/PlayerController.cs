using UnityEngine;

/// <summary>
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║         PLAYER CONTROLLER 2D — Ultimate Speedrun Edition        ║
/// ║                                                                  ║
/// ║  Techniques implémentées :                                       ║
/// ║  • Jump Buffering (amélioré)          • Corner Correction        ║
/// ║  • Ledge Forgiveness                  • Variable Gravity         ║
/// ║  • Jump Arc Control                   • Dynamic Accel Curves     ║
/// ║  • Player Responsiveness              • Input Latency Reduction  ║
/// ║  • Subpixel Movement                  • Coyote Time              ║
/// ║  • Apex Modifier                      • Jump Cut                 ║
/// ║  • Wall Jump / Wall Slide             • Double Jump              ║
/// ║  • Dash                               • Squash & Stretch         ║
/// ║  • Turn Boost                         • Air Momentum             ║
/// ╚══════════════════════════════════════════════════════════════════╝
///
/// Setup :
///   1. Attacher sur le Player avec Rigidbody2D + CapsuleCollider2D
///   2. Rigidbody2D → Interpolate, Collision Detection → Continuous
///   3. Créer un Layer "Ground", l'assigner aux plateformes
///   4. Renseigner Ground Layer dans l'Inspector
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //   ■ CAPACITÉS (ON / OFF dans l'Inspector)
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  CAPACITÉS DÉBLOQUÉES  ━━━━━━━━━━")]
    [Tooltip("Active le Wall Jump et le Wall Slide")]
    public bool wallMechanicsEnabled = false;

    [Tooltip("Active le Double Jump (saut en l'air)")]
    public bool doubleJumpEnabled = false;

    [Tooltip("Active le Dash (Shift gauche/droit)")]
    public bool dashEnabled = false;

    // ══════════════════════════════════════════════════════════════
    //   ■ HORIZONTAL
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  HORIZONTAL  ━━━━━━━━━━━━━━━━━━━━")]
    [Tooltip("Vitesse maximale de déplacement (unités/s)")]
    public float maxSpeed = 11f;

    [Tooltip("Vitesse maximale quand on court sur le sol (peut dépasser maxSpeed brièvement)")]
    public float runTopSpeed = 13f;

    [Tooltip("Taux d'accélération de base au sol")]
    public float groundAccel = 120f;

    [Tooltip("Taux de décélération (frottement) au sol")]
    public float groundDecel = 100f;

    [Tooltip("Taux d'accélération dans les airs")]
    public float airAccel = 75f;

    [Tooltip("Taux de décélération dans les airs — faible pour conserver l'élan")]
    public float airDecel = 30f;

    [Tooltip("Multiplicateur de force lors d'un demi-tour (réactivité instantanée)")]
    [Range(1f, 6f)]
    public float turnBoost = 3.5f;

    [Header("• Dynamic Acceleration Curves")]
    [Tooltip("Courbe d'accélération au sol : X=vitesse normalisée [0-1], Y=multiplicateur d'accel")]
    public AnimationCurve groundAccelCurve = AnimationCurve.EaseInOut(0f, 1.4f, 1f, 0.7f);

    [Tooltip("Courbe d'accélération en l'air : X=vitesse normalisée [0-1], Y=multiplicateur")]
    public AnimationCurve airAccelCurve = AnimationCurve.EaseInOut(0f, 1.2f, 1f, 0.5f);

    [Tooltip("Courbe de décélération : X=vitesse normalisée, Y=multiplicateur de friction")]
    public AnimationCurve decelCurve = AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.5f);

    // ══════════════════════════════════════════════════════════════
    //   ■ SAUT & GRAVITÉ
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  SAUT & GRAVITÉ  ━━━━━━━━━━━━━━━━")]
    [Tooltip("Hauteur maximale du saut (unités Unity). Vélocité calculée via v=√(2gh)")]
    public float jumpHeight = 4.5f;

    [Tooltip("Gravité de base appliquée manuellement")]
    public float gravity = 34f;

    [Header("• Variable Gravity — Jump Arcs")]
    [Tooltip("Multiplicateur de gravité en montée (< 1 = montée plus longue et flottante)")]
    [Range(0.1f, 2f)]
    public float riseGravityMult = 0.85f;

    [Tooltip("Multiplicateur de gravité en descente (> 1 = chute rapide et précise)")]
    [Range(1f, 5f)]
    public float fallGravityMult = 2.5f;

    [Tooltip("Multiplicateur si le bouton saut est relâché tôt (saut court)")]
    [Range(1f, 12f)]
    public float jumpCutMult = 6f;

    [Tooltip("Multiplicateur à l'apex du saut (flottement)")]
    [Range(0f, 1f)]
    public float apexGravityMult = 0.35f;

    [Tooltip("Fenêtre de vitesse Y autour de 0 considérée comme apex (unités/s)")]
    public float apexThreshold = 3.5f;

    [Tooltip("Bonus de contrôle horizontal à l'apex")]
    [Range(0f, 1.5f)]
    public float apexSpeedBonus = 0.65f;

    [Tooltip("Vitesse de chute maximale absolue")]
    public float maxFallSpeed = 30f;

    [Tooltip("Nombre de sauts aériens (double jump = 1, triple = 2…)")]
    [Range(0, 3)]
    public int airJumps = 1;

    [Tooltip("Multiplicateur de hauteur des sauts aériens")]
    [Range(0.5f, 1f)]
    public float airJumpMult = 0.85f;

    // ══════════════════════════════════════════════════════════════
    //   ■ FEEL — COYOTE / BUFFER / LEDGE / CORNER
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  FEEL & RESPONSIVENESS  ━━━━━━━━━")]
    [Tooltip("Jump Buffer : mémoriser la pression saut avant l'atterrissage (s)")]
    [Range(0f, 0.3f)]
    public float jumpBufferTime = 0.15f;

    [Tooltip("Coyote Time : fenêtre de saut après avoir quitté un bord (s)")]
    [Range(0f, 0.25f)]
    public float coyoteTime = 0.13f;

    [Header("• Corner Correction")]
    [Tooltip("Activer la correction de coin (le joueur passe les arêtes sans bloquer)")]
    public bool cornerCorrection = true;

    [Tooltip("Distance max de correction latérale (unités)")]
    [Range(0.05f, 0.6f)]
    public float cornerCorrDist = 0.35f;

    [Tooltip("Nombre de rayons utilisés pour la détection des coins")]
    [Range(2, 8)]
    public int cornerCorrRays = 4;

    [Header("• Ledge Forgiveness")]
    [Tooltip("Activer la détection de bord : ramener le joueur sur la plateforme si presque sur le bord")]
    public bool ledgeForgiveness = true;

    [Tooltip("Distance horizontale de détection du bord")]
    [Range(0.05f, 0.4f)]
    public float ledgeForgiveRange = 0.18f;

    [Header("• Input Latency Reduction")]
    [Tooltip("Lire le saut en Update (frame-perfect) plutôt qu'en FixedUpdate")]
    public bool updateInputPriority = true;

    [Tooltip("Délai d'antirebond input (évite double-saut accidentel, en secondes)")]
    [Range(0f, 0.05f)]
    public float inputDebounce = 0.016f;

    [Header("• Subpixel Movement")]
    [Tooltip("Activer l'accumulation subpixel pour un mouvement lisse à toute vitesse")]
    public bool subpixelMovement = true;

    [Tooltip("Taille du pixel en unités Unity (ex: 1/16 pour un pixel art 16 PPU)")]
    public float pixelSize = 0.0625f; // 1/16

    [Header("• Squash & Stretch")]
    public bool squashStretch = true;

    [Range(0f, 0.4f)]
    public float squashIntensity = 0.2f;

    // ══════════════════════════════════════════════════════════════
    //   ■ WALL
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  WALL JUMP / WALL SLIDE  ━━━━━━━━")]
    public float wallJumpForceX = 9.5f;

    [Range(0f, 0.5f)]
    public float wallJumpLock = 0.22f;

    [Range(0f, 0.8f)]
    public float wallSlideGrav = 0.22f;

    // ══════════════════════════════════════════════════════════════
    //   ■ DASH
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  DASH  ━━━━━━━━━━━━━━━━━━━━━━━━━━")]
    public float dashSpeed = 26f;

    [Range(0.05f, 0.3f)]
    public float dashDuration = 0.10f;

    [Range(0.1f, 2f)]
    public float dashCooldown = 0.55f;

    [Range(0f, 2f)]
    public float dashExitMomentum = 1.25f;

    // ══════════════════════════════════════════════════════════════
    //   ■ COLLISION DETECTION
    // ══════════════════════════════════════════════════════════════

    [Header("━━━━━━━━━━  COLLISION DETECTION  ━━━━━━━━━━━")]
    public LayerMask groundLayer;
    public Vector2 groundCheckOffset = new Vector2(0f, -0.52f);
    public Vector2 groundCheckSize = new Vector2(0.42f, 0.08f);
    public Vector2 wallCheckOffR = new Vector2(0.28f, 0f);
    public Vector2 wallCheckOffL = new Vector2(-0.28f, 0f);
    public Vector2 wallCheckSize = new Vector2(0.06f, 0.38f);

    [Tooltip("Hauteur de départ des rayons corner correction (depuis le dessus du collider)")]
    public float colliderHalfH = 0.55f;

    [Tooltip("Demi-largeur du collider (pour les corner rays)")]
    public float colliderHalfW = 0.22f;

    // ══════════════════════════════════════════════════════════════
    //   ÉTAT INTERNE PRIVÉ
    // ══════════════════════════════════════════════════════════════

    private Rigidbody2D _rb;
    private Vector2 _vel;           // Vélocité courante (travail en FixedUpdate)

    // Collision
    private bool _grounded, _wasGrounded;
    private bool _onWall;
    private float _wallDir;             // +1 mur à droite, -1 mur à gauche

    // Timers
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _wallJumpTimer;
    private float _dashCooldownTimer;

    // Saut
    private bool _isJumping;
    private bool _jumpCut;
    private int _airJumpsLeft;
    private bool _atApex;

    // Wall
    private bool _wallSliding;

    // Dash
    private bool _isDashing;
    private float _dashTimer;
    private int _dashesLeft;
    private float _dashDirX;

    // ── Input ──
    // Lus en Update (frame-perfect), consommés en FixedUpdate
    private float _inputX;
    private bool _jumpConsumed;        // true si déjà traité ce frame
    private bool _jumpPressedThisFrame;
    private bool _jumpHeld;
    private bool _jumpReleasedThisFrame;
    private bool _dashPressedThisFrame;
    private float _debounceTimer;

    // Subpixel
    private Vector2 _subpixelAccum;     // Accumulation fractionnelle

    // Squash & Stretch
    private Vector3 _baseScale;
    private Vector3 _targetScale;

    // ══════════════════════════════════════════════════════════════
    //   AWAKE / START
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;   // Gravité 100% manuelle
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

    // ══════════════════════════════════════════════════════════════
    //   UPDATE — Lecture des inputs (chaque frame, pas FixedUpdate)
    //
    //   INPUT LATENCY REDUCTION :
    //   Lire les inputs en Update garantit qu'on ne rate jamais
    //   une frame. Un input pressé en Update est stocké et consommé
    //   au prochain FixedUpdate, réduisant la latence à ~0 frame
    //   perçue. Sans ça, un saut pressé juste après un FixedUpdate
    //   attend jusqu'au suivant (+16ms à 60fps).
    // ══════════════════════════════════════════════════════════════

    private void Update()
    {
        _inputX = Input.GetAxisRaw("Horizontal");
        _jumpHeld = Input.GetButton("Jump");

        // Input debounce : évite double-détection sur un seul appui
        _debounceTimer -= Time.deltaTime;

        if (Input.GetButtonDown("Jump") && _debounceTimer <= 0f)
        {
            _jumpPressedThisFrame = true;
            _debounceTimer = inputDebounce;
            // Jump buffer : mémoriser le saut pour les prochains FixedUpdate
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

        // Jump cut en Update (réactivité maximale)
        if (_jumpReleasedThisFrame && _isJumping && _vel.y > 0f)
            _jumpCut = true;

        if (squashStretch)
            UpdateSquashStretch();
    }

    // ══════════════════════════════════════════════════════════════
    //   FIXED UPDATE — Simulation physique
    // ══════════════════════════════════════════════════════════════

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
        ApplySubpixelMovement();

        // Consommer les flags one-shot
        _jumpPressedThisFrame = false;
        _jumpReleasedThisFrame = false;
        _dashPressedThisFrame = false;
    }

    // ══════════════════════════════════════════════════════════════
    //   1. DÉTECTION DES COLLISIONS
    // ══════════════════════════════════════════════════════════════

    private void CheckCollisions()
    {
        _wasGrounded = _grounded;

        _grounded = Physics2D.OverlapBox(
            (Vector2)transform.position + groundCheckOffset,
            groundCheckSize, 0f, groundLayer);

        // Murs
        _onWall = false;
        if (wallMechanicsEnabled)
        {
            bool r = Physics2D.OverlapBox(
                (Vector2)transform.position + wallCheckOffR, wallCheckSize, 0f, groundLayer);
            bool l = Physics2D.OverlapBox(
                (Vector2)transform.position + wallCheckOffL, wallCheckSize, 0f, groundLayer);

            if (r) { _onWall = true; _wallDir = 1f; }
            else if (l) { _onWall = true; _wallDir = -1f; }
        }

        // LEDGE FORGIVENESS :
        // Si le joueur est très légèrement au-delà du bord d'une plateforme
        // mais que la moitié du collider est encore dessus, on étend le sol.
        // Évite les "chutes de bord" accidentelles quand le joueur dépasse de 1px.
        if (!_grounded && _wasGrounded && ledgeForgiveness && Mathf.Abs(_vel.x) < 0.1f)
        {
            bool ledgeR = Physics2D.OverlapBox(
                (Vector2)transform.position + groundCheckOffset + new Vector2(ledgeForgiveRange, 0f),
                groundCheckSize, 0f, groundLayer);
            bool ledgeL = Physics2D.OverlapBox(
                (Vector2)transform.position + groundCheckOffset - new Vector2(ledgeForgiveRange, 0f),
                groundCheckSize, 0f, groundLayer);

            if (ledgeR || ledgeL)
                _grounded = true;
        }

        // Atterrissage → reset ressources
        if (!_wasGrounded && _grounded)
        {
            _isJumping = false;
            _jumpCut = false;
            _airJumpsLeft = airJumps;
            _dashesLeft = 1;

            if (squashStretch)
                _targetScale = new Vector3(
                    _baseScale.x * (1f + squashIntensity * 1.6f),
                    _baseScale.y * (1f - squashIntensity * 1.6f),
                    _baseScale.z);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //   2. TIMERS
    // ══════════════════════════════════════════════════════════════

    private void TickTimers()
    {
        float dt = Time.fixedDeltaTime;

        // Coyote time : démarre dès qu'on quitte le sol sans avoir sauté
        if (_wasGrounded && !_grounded && !_isJumping)
            _coyoteTimer = coyoteTime;
        else
            _coyoteTimer -= dt;

        _jumpBufferTimer -= dt;
        _wallJumpTimer -= dt;
        _dashCooldownTimer -= dt;
    }

    // ══════════════════════════════════════════════════════════════
    //   3. GRAVITÉ — Variable Gravity System
    //
    //   Chaque phase du saut a sa propre gravité :
    //
    //   ① MONTÉE (vel.y > apex)    → gravité × riseGravityMult (< 1)
    //      Montée longue, saut planant. Bon pour les puzzles de saut.
    //
    //   ② APEX (|vel.y| < seuil)   → gravité × apexGravityMult (≪ 1)
    //      Flottement au sommet, contrôle horizontal maximal.
    //
    //   ③ CHUTE (vel.y < 0)        → gravité × fallGravityMult (> 1)
    //      Chute rapide et précise, feel "poids", atterrissages nets.
    //
    //   ④ JUMP CUT                 → gravité × jumpCutMult (élevé)
    //      Arrête la montée immédiatement si le bouton est relâché.
    //
    //   ⑤ WALL SLIDE               → gravité × wallSlideGrav (faible)
    //      Descente lente sur les murs.
    // ══════════════════════════════════════════════════════════════

    private void HandleGravity()
    {
        // Coller au sol : légère vélocité négative constante (anti-bounce)
        if (_grounded && !_isJumping)
        {
            _vel.y = Mathf.MoveTowards(_vel.y, -2f, gravity * Time.fixedDeltaTime);
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

    // ══════════════════════════════════════════════════════════════
    //   4. HORIZONTAL — Dynamic Acceleration Curves
    //
    //   Au lieu d'un taux d'accélération fixe, on utilise des
    //   AnimationCurves qui adaptent l'accélération selon la vitesse
    //   actuelle normalisée [0→1].
    //
    //   Exemple : au départ (v≈0) → accel élevée (punch de démarrage)
    //             à pleine vitesse (v≈max) → accel réduite (plateau)
    //
    //   Pour la décélération : plus on va vite, plus la friction est
    //   forte → arrêt progressif mais réactif.
    //
    //   Lerp exponentiel : v += (target - v) * rate * dt
    //   → convergence asymptotique, jamais d'oscillation
    // ══════════════════════════════════════════════════════════════

    private void HandleHorizontal()
    {
        // Blocage post wall-jump
        if (_wallJumpTimer > 0f) return;

        float target = _inputX * maxSpeed;
        bool hasInput = Mathf.Abs(_inputX) > 0.01f;
        bool turning = hasInput
                         && Mathf.Sign(_inputX) != Mathf.Sign(_vel.x)
                         && Mathf.Abs(_vel.x) > 0.1f;

        // Vitesse normalisée [0,1] pour échantillonner les courbes
        float speedNorm = Mathf.Abs(_vel.x) / Mathf.Max(runTopSpeed, 0.01f);
        speedNorm = Mathf.Clamp01(speedNorm);

        float rate;

        if (hasInput)
        {
            // Accélération : modulée par la courbe selon contexte sol/air
            float baseAccel = _grounded ? groundAccel : airAccel;
            float curveMult = _grounded
                ? groundAccelCurve.Evaluate(speedNorm)
                : airAccelCurve.Evaluate(speedNorm);

            rate = baseAccel * curveMult;

            // Demi-tour : turnBoost pour une réactivité instantanée
            if (turning) rate *= turnBoost;

            // Bonus de contrôle à l'apex du saut
            if (_atApex) target = _inputX * maxSpeed * (1f + apexSpeedBonus);
        }
        else
        {
            // Décélération : plus on va vite, plus la friction est forte
            float baseDecel = _grounded ? groundDecel : airDecel;
            float curveMult = decelCurve.Evaluate(speedNorm);
            rate = baseDecel * curveMult;
        }

        // Lerp exponentiel → smooth, sans dépassement, sans oscillation
        _vel.x += (target - _vel.x) * rate * Time.fixedDeltaTime;

        // Clamp
        float maxVel = _atApex ? maxSpeed * (1f + apexSpeedBonus) : runTopSpeed;
        _vel.x = Mathf.Clamp(_vel.x, -maxVel, maxVel);
    }

    // ══════════════════════════════════════════════════════════════
    //   5. SAUT — Jump Buffering + Corner Correction
    // ══════════════════════════════════════════════════════════════

    private void HandleJump()
    {
        bool bufferActive = _jumpBufferTimer > 0f;
        bool canGround = bufferActive && (_grounded || _coyoteTimer > 0f);
        bool canWall = bufferActive && _onWall && !_grounded && wallMechanicsEnabled;
        bool canAir = _jumpPressedThisFrame && !_grounded && _airJumpsLeft > 0 && doubleJumpEnabled;

        if (canGround)
        {
            DoJump(jumpHeight);
        }
        else if (canWall)
        {
            DoWallJump();
        }
        else if (canAir)
        {
            _airJumpsLeft--;
            DoJump(jumpHeight * airJumpMult);
        }
    }

    // CORNER CORRECTION :
    // Quand le joueur saute et que la tête touche un coin (1-2px de
    // débordement sur le côté d'un bloc), on vérifie avec N rayons si
    // une correction latérale permettrait de passer librement.
    // Si oui, on déplace silencieusement le joueur horizontalement.
    // Résultat : plus aucun saut bloqué par une arête invisible.
    private bool TryCornerCorrect(float jumpVelocity)
    {
        if (!cornerCorrection || jumpVelocity <= 0f) return false;

        float step = (colliderHalfW * 2f) / (cornerCorrRays - 1);
        Vector2 origin = (Vector2)transform.position + new Vector2(-colliderHalfW, colliderHalfH);

        for (int i = 0; i < cornerCorrRays; i++)
        {
            Vector2 rayOrigin = origin + new Vector2(step * i, 0f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up, 0.2f, groundLayer);

            if (!hit) continue; // Ce rayon est libre

            // Il y a un obstacle, chercher si un décalage latéral aide
            for (float offset = pixelSize; offset <= cornerCorrDist; offset += pixelSize)
            {
                // Essai à droite
                bool clearR = !Physics2D.OverlapBox(
                    (Vector2)transform.position + new Vector2(offset, 0f) + groundCheckOffset,
                    groundCheckSize, 0f, groundLayer);

                // Vérifier aussi qu'il n'y a pas de plafond à droite
                bool headClearR = !Physics2D.OverlapBox(
                    (Vector2)transform.position + new Vector2(offset, colliderHalfH),
                    new Vector2(colliderHalfW * 2f, 0.05f), 0f, groundLayer);

                if (clearR && headClearR)
                {
                    transform.position += new Vector3(offset, 0f, 0f);
                    return true;
                }

                // Essai à gauche
                bool clearL = !Physics2D.OverlapBox(
                    (Vector2)transform.position + new Vector2(-offset, 0f) + groundCheckOffset,
                    groundCheckSize, 0f, groundLayer);

                bool headClearL = !Physics2D.OverlapBox(
                    (Vector2)transform.position + new Vector2(-offset, colliderHalfH),
                    new Vector2(colliderHalfW * 2f, 0.05f), 0f, groundLayer);

                if (clearL && headClearL)
                {
                    transform.position += new Vector3(-offset, 0f, 0f);
                    return true;
                }
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

        float v = Mathf.Sqrt(2f * gravity * height);   // v = √(2gh)
        TryCornerCorrect(v);
        _vel.y = v;

        if (squashStretch)
            _targetScale = new Vector3(
                _baseScale.x * (1f - squashIntensity * 0.9f),
                _baseScale.y * (1f + squashIntensity * 1.2f),
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

    // ══════════════════════════════════════════════════════════════
    //   6. WALL SLIDE
    // ══════════════════════════════════════════════════════════════

    private void HandleWallSlide()
    {
        _wallSliding = wallMechanicsEnabled
                       && _onWall
                       && !_grounded
                       && _vel.y < 0f
                       && _inputX * _wallDir > 0f;
    }

    // ══════════════════════════════════════════════════════════════
    //   7. DASH
    // ══════════════════════════════════════════════════════════════

    private void TryDash()
    {
        if (!dashEnabled || !_dashPressedThisFrame
            || _dashesLeft <= 0 || _dashCooldownTimer > 0f) return;

        _dashesLeft--;
        _isDashing = true;
        _dashTimer = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _jumpCut = false;
        _dashDirX = _inputX != 0f
            ? Mathf.Sign(_inputX)
            : (transform.localScale.x >= 0f ? 1f : -1f);

        _vel = new Vector2(_dashDirX * dashSpeed, 0f);
    }

    private void TickDashMotion()
    {
        _dashTimer -= Time.fixedDeltaTime;
        float t = 1f - (_dashTimer / dashDuration); // 0→1 pendant le dash

        // Easing out : légère décélération en fin de dash
        _vel = new Vector2(_dashDirX * Mathf.Lerp(dashSpeed, dashSpeed * 0.8f, t * t), 0f);

        if (_dashTimer <= 0f)
        {
            _isDashing = false;
            _vel.y = 0f;
            _vel.x = _dashDirX * maxSpeed * dashExitMomentum; // Élan conservé
        }
    }

    // ══════════════════════════════════════════════════════════════
    //   8. SUBPIXEL MOVEMENT
    //
    //   Le moteur physics applique des positions en float mais
    //   l'affichage pixel-art arrondit à la grille de pixels.
    //   Sans gestion subpixel : "glissement" irrégulier sur les
    //   petites vitesses (ex : vel.x = 0.03f → bouge aléatoirement).
    //
    //   Technique :
    //   • On accumule la vélocité fractionnelle séparément.
    //   • On n'applique à _rb qu'une vélocité alignée sur la grille.
    //   • La fraction restante est mémorisée pour le prochain frame.
    //   → Déplacement parfaitement lisse même à 1px/s.
    // ══════════════════════════════════════════════════════════════

    private void ApplySubpixelMovement()
    {
        if (!subpixelMovement)
        {
            _rb.linearVelocity = _vel;
            return;
        }

        float dt = Time.fixedDeltaTime;

        // Déplacement souhaité ce frame (en unités)
        Vector2 delta = _vel * dt;

        // Additionner l'accumulation subpixel
        _subpixelAccum += delta;

        // Ne déplacer que d'un nombre entier de pixels
        float pixelsX = Mathf.Floor(Mathf.Abs(_subpixelAccum.x) / pixelSize)
                        * pixelSize * Mathf.Sign(_subpixelAccum.x);
        float pixelsY = Mathf.Floor(Mathf.Abs(_subpixelAccum.y) / pixelSize)
                        * pixelSize * Mathf.Sign(_subpixelAccum.y);

        // Retirer ce qu'on a "consommé" de l'accumulation
        _subpixelAccum.x -= pixelsX;
        _subpixelAccum.y -= pixelsY;

        // Convertir en vélocité pour ce frame (Rigidbody2D attend units/s)
        Vector2 snappedVel = new Vector2(
            dt > 0f ? pixelsX / dt : 0f,
            dt > 0f ? pixelsY / dt : 0f
        );

        // Garder la vélocité physique réelle pour la logique interne
        // mais appliquer la version snappée à l'objet
        _rb.linearVelocity = snappedVel;
    }

    // ══════════════════════════════════════════════════════════════
    //   9. SQUASH & STRETCH
    //   Conservation du volume : scaleX = 1 / scaleY
    // ══════════════════════════════════════════════════════════════

    private void UpdateSquashStretch()
    {
        if (!_grounded && !_isDashing)
        {
            float norm = Mathf.Clamp(_vel.y / Mathf.Sqrt(2f * gravity * jumpHeight), -1f, 1f);
            float stretchY = 1f + norm * squashIntensity;
            float squashX = 1f / Mathf.Max(stretchY, 0.01f);

            _targetScale = new Vector3(
                _baseScale.x * squashX,
                _baseScale.y * stretchY,
                _baseScale.z);
        }
        else if (_grounded && Mathf.Abs(_vel.x) > 0.5f)
        {
            // Légère déformation en course
            _targetScale = new Vector3(
                _baseScale.x * (1f + squashIntensity * 0.12f),
                _baseScale.y * (1f - squashIntensity * 0.08f),
                _baseScale.z);
        }
        else if (_grounded)
        {
            _targetScale = _baseScale;
        }

        // Lerp smooth vers la cible
        transform.localScale = Vector3.Lerp(
            transform.localScale, _targetScale, Time.deltaTime * 20f);
    }

    // ══════════════════════════════════════════════════════════════
    //   API PUBLIQUE — débloquer depuis GameManager / Zone / Trigger
    // ══════════════════════════════════════════════════════════════

    public void UnlockWall() => wallMechanicsEnabled = true;
    public void UnlockDoubleJump() => doubleJumpEnabled = true;
    public void UnlockDash() => dashEnabled = true;
    public void LockAll() => wallMechanicsEnabled = doubleJumpEnabled = dashEnabled = false;
    public void UnlockAll() => wallMechanicsEnabled = doubleJumpEnabled = dashEnabled = true;

    // ══════════════════════════════════════════════════════════════
    //   GIZMOS — Visualiser les capteurs dans la Scene View
    // ══════════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        // Sol
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + (Vector3)groundCheckOffset, groundCheckSize);

        // Murs
        if (wallMechanicsEnabled)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + (Vector3)wallCheckOffR, wallCheckSize);
            Gizmos.DrawWireCube(transform.position + (Vector3)wallCheckOffL, wallCheckSize);
        }

        // Corner correction rays
        if (cornerCorrection)
        {
            Gizmos.color = Color.yellow;
            float step = (colliderHalfW * 2f) / (cornerCorrRays - 1);
            Vector3 start = transform.position + new Vector3(-colliderHalfW, colliderHalfH, 0f);
            for (int i = 0; i < cornerCorrRays; i++)
            {
                Vector3 o = start + new Vector3(step * i, 0f, 0f);
                Gizmos.DrawLine(o, o + Vector3.up * 0.2f);
            }
        }

        // Ledge forgiveness
        if (ledgeForgiveness)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(
                transform.position + (Vector3)(groundCheckOffset + new Vector2(ledgeForgiveRange, 0f)),
                groundCheckSize);
            Gizmos.DrawWireCube(
                transform.position + (Vector3)(groundCheckOffset - new Vector2(ledgeForgiveRange, 0f)),
                groundCheckSize);
        }
    }
}
