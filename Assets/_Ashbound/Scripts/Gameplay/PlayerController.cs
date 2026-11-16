using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [SerializeField] Transform visual;
    [Tooltip("Tinted with the chosen class color")] [SerializeField] SpriteRenderer[] classTinted;
    [Tooltip("Shows the chosen class's character art")] [SerializeField] SpriteRenderer heroSprite;

    PlayerStats stats;
    Rigidbody2D rb;
    InputAction move, pad;
    Vector2 keysRest, padRest;
    bool restRead, keysActive, padActive;
    Vector2 input;
    float bobT, facingSign = 1f;

    public PlayerStats Stats => stats;
    public Vector2 Facing { get; private set; } = Vector2.right;

    void Awake()
    {
        Instance = this;
        stats = GetComponent<PlayerStats>();
        rb = GetComponent<Rigidbody2D>();

        move = new InputAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        // Gamepad is separate: some USB/Bluetooth devices report as a gamepad with a stick stuck at -1.
        // A source only counts once it moves away from the value it had at the start.
        pad = new InputAction("Pad", InputActionType.Value, "<Gamepad>/leftStick", processors: "stickDeadzone");
    }

    void Start()
    {
        var cls = RunSelection.Class;
        if (cls == null) return;
        if (heroSprite != null && cls.portrait != null) heroSprite.sprite = cls.portrait;
        if (classTinted != null)
            foreach (var r in classTinted) stats.SetBaseColor(r, cls.color);
    }

    void OnEnable() { move.Enable(); pad.Enable(); }

    // Drop any key the OS thinks is still held (e.g. released while the window was unfocused).
    void OnApplicationFocus(bool focused)
    {
        if (!focused) return;
        if (Keyboard.current != null) InputSystem.ResetDevice(Keyboard.current);
        move.Reset();
        pad.Reset();
    }
    void OnDisable() { move.Disable(); pad.Disable(); }

    void OnDestroy()
    {
        move.Dispose();
        pad.Dispose();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        Vector2 k = move.ReadValue<Vector2>(), p = pad.ReadValue<Vector2>();
        if (!restRead) { keysRest = k; padRest = p; restRead = true; }
        if (!keysActive && (k - keysRest).sqrMagnitude > 0.16f) keysActive = true;
        if (!padActive && (p - padRest).sqrMagnitude > 0.16f) padActive = true;
        Vector2 raw = keysActive && k.sqrMagnitude > 0.01f ? k : padActive ? p : Vector2.zero;
        input = stats.IsDead ? Vector2.zero : Vector2.ClampMagnitude(raw, 1f);
        if (input.sqrMagnitude > 0.01f) Facing = input.normalized;
        if (Mathf.Abs(input.x) > 0.05f) facingSign = Mathf.Sign(input.x);

        if (visual == null) return;
        bool moving = input.sqrMagnitude > 0.01f;
        bobT += Time.deltaTime * (moving ? 14f : 3f);
        float b = Mathf.Sin(bobT);
        float squash = moving ? b * 0.06f : b * 0.02f;
        visual.localPosition = new Vector3(0f, moving ? Mathf.Abs(b) * 0.08f : 0f, 0f);
        visual.localScale = new Vector3(facingSign * (1f + squash), 1f - squash, 1f);
        visual.localRotation = Quaternion.Euler(0f, 0f, -input.x * 6f);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + input * stats.MoveSpeed * Time.fixedDeltaTime);
    }
}
