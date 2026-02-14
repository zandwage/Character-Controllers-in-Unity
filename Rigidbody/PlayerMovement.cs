using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(TransformInterpolator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform cam;

    [Header("Functional Options")]
    public bool canMove = true;

    [Header("Movement Settings")]
    public float walkSpeed = 3.9f;
    public float sprintSpeed = 5.9f;
    public float crouchSpeed = 2.1f;
    public float crouchSmooth = 12.5f;
    public float desiredCrouchHeight = 0.55f;
    public float jumpForce = 24f;
    public float groundDrag = 14.85f;
    public float airDrag = 2.5f;
    public float movementMultiplier = 30f; 
    public float gravityMultiplier = 40f;    
    public float airMultiplier = 0.115f;    

    [Header("Slope Settings")]
    [Range(1f, 89f)]
    public float maxSlopeAngle = 45f;

    [Header("Headbob Settings")]
    public float headbobSmooth = 7.5f;
    public float walkBobSpeed = 10.85f;
    public float walkBobAmount = 0.1f;
    public float sprintBobSpeed = 14.75f;
    public float sprintBobAmount = 0.125f;
    public float crouchBobSpeed = 7.25f;
    public float crouchBobAmount = 0.1f;
 
    [Header("Ground Check Settings")]
    public LayerMask whatIsPlayer;

    [Header("Debugging")] 
    public float fallTimer;
    public float landThreshold;
    public bool grounded;
    public bool groundedLastFrame;    
    public bool justLanded;
    public bool onSlope;
    public bool moving;
    public bool jumping;
    public bool crouching;
    public bool sprinting;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Vector3 playerInput;
    
    private Vector3 moveDirection;

    private Vector3 slopeMoveDirection;
    private Vector3 headbobOffset;
    
    private float moveSpeed;

    private float standCamPos, crouchCamPos;
    private float standHeight, crouchHeight;
    private float standCenter, crouchCenter;

    private float currentSlopeAngle;

    private RaycastHit slopeHit;

    private int stepsSinceLastGrounded;
    private int stepsSinceLastJump;
    private bool walkingForwards;

    private Vector3 camPos;
    private Vector3 currentCenter;
    private float currentHeight;

    private float currentSpeed;
    private float currentAmount;

    private bool crouchingUnderSomething;

    private Vector3 groundCheckPos;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        standCamPos = cam.transform.localPosition.y;
        crouchCamPos = standCamPos * desiredCrouchHeight;
        standHeight = capsule.height;
        crouchHeight = standHeight * desiredCrouchHeight;
        standCenter = capsule.center.y;
        crouchCenter = standCenter * desiredCrouchHeight;    

        groundedLastFrame = true;
    }

    void Update()
    {        
        SetGrounded();
        SetLandBool();          
        SetCrouchUnderSomethingBool();
        SetOnSlope();

        if (canMove)
            MyInput();

        UpdateDrag();

        if (!grounded && !SnapToGround())
            stepsSinceLastGrounded += 1;
        else if (grounded || SnapToGround())
            stepsSinceLastGrounded = 0;
    }

    void FixedUpdate()
    {
        SetFallTimer();
        UpdateState();        

        if (canMove)
            Movement();
    }
    
    void SetGrounded()
    {
        groundCheckPos = new Vector3(capsule.bounds.extents.x / 2f, capsule.bounds.extents.y * 1.075f, capsule.bounds.extents.z / 2f);
        grounded = currentSlopeAngle < maxSlopeAngle && Physics.CheckBox(capsule.bounds.center, groundCheckPos, Quaternion.identity, ~whatIsPlayer);
    }

    void SetOnSlope()
    {
        onSlope = OnSlope();        
    }

    void SetFallTimer()
    {
        fallTimer = (grounded ? 0f : fallTimer += Time.deltaTime);               
    }

    void SetCrouchUnderSomethingBool()
    {
        crouchingUnderSomething = Physics.Raycast(cam.position, Vector3.up, 0.75f);        
    }

    #region Movement
    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
    }

    void UpdateDrag()
    {
        rb.drag = grounded ? groundDrag : airDrag;        
    }

    bool SnapToGround()
    {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
            return false;

        if (!Physics.Raycast(rb.position, Vector3.down, out RaycastHit hit))
            return false;

        float speed = rb.velocity.magnitude;
        float dot = Vector3.Dot(rb.velocity, hit.normal);

        if (dot > 0f)
            rb.velocity = (rb.velocity - hit.normal * dot).normalized * speed;

        return false;
    }

    void MyInput()
    {
        playerInput = new Vector3(
            Input.GetKey(KeyCode.D) ? 1f : Input.GetKey(KeyCode.A) ? -1f : 0f,
            0f,
            Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f
        );

        moveDirection = transform.TransformDirection(playerInput);

        slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;

        moving = Mathf.Abs(moveDirection.x) > 0.01f || Mathf.Abs(moveDirection.z) > 0.01f;

        walkingForwards = playerInput.z == 1f;

        jumping = Input.GetKeyDown(KeyCode.Space) && !crouchingUnderSomething;
        crouching = Input.GetKey(KeyCode.LeftControl) || crouchingUnderSomething;
        sprinting = Input.GetKey(KeyCode.LeftShift) && walkingForwards;        

        moveSpeed = crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;

        Jump(jumping);
        Crouch(crouching);

        Headbob();
    }

    void Movement()
    {
        if (!grounded)
            rb.AddForce(Vector3.down * gravityMultiplier * Time.deltaTime, ForceMode.Impulse);        

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            if (grounded && !onSlope)
                rb.AddForce(moveDirection.normalized * moveSpeed * movementMultiplier * Time.fixedDeltaTime, ForceMode.VelocityChange);
            else if (grounded && onSlope)
            {
                rb.AddForce(slopeMoveDirection.normalized * moveSpeed * movementMultiplier * Time.fixedDeltaTime, ForceMode.VelocityChange);
            }
            else if (!grounded)
                rb.AddForce(moveDirection.normalized * moveSpeed * movementMultiplier * Time.fixedDeltaTime * airMultiplier, ForceMode.VelocityChange);            
        }

        rb.useGravity = !moving && onSlope ? false : true;   
    }
    #endregion

    #region Actions
    void Jump(bool jumping)
    {
        if (grounded && jumping)
        {
            stepsSinceLastJump = 0;

            rb.velocity = new Vector3(rb.velocity.x, jumpForce, rb.velocity.z);
        }
    }

    void Crouch(bool crouching)
    {
        camPos = new Vector3(cam.localPosition.x, crouching ? crouchCamPos : standCamPos, cam.localPosition.z);
        currentCenter = new Vector3(capsule.center.x, crouching ? crouchCenter : standCenter, capsule.center.z);
        currentHeight = (crouching ? crouchHeight : standHeight);

        cam.localPosition = Vector3.Lerp(cam.localPosition, camPos, crouchSmooth * Time.deltaTime);
        capsule.center = Vector3.Lerp(capsule.center, currentCenter, crouchSmooth * Time.deltaTime);
        capsule.height = Mathf.Lerp(capsule.height, currentHeight, crouchSmooth * Time.deltaTime);
    }
    #endregion

    #region Checks
    bool OnSlope()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, capsule.height * 0.5f + 0.3f))
        {
            currentSlopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return currentSlopeAngle < maxSlopeAngle && currentSlopeAngle != 0f;
        }
        return false;
    }

    void SetLandBool()
    {
        if (grounded && (!groundedLastFrame && fallTimer >= landThreshold))
        {
            justLanded = true;
        }
        else if (groundedLastFrame)
            justLanded = false;

        groundedLastFrame = grounded;
    }    

    #endregion

    #region Effects
    void Headbob()
    {
        if (grounded && moving)
        {
            currentSpeed += Time.deltaTime * (crouching ? crouchBobSpeed : sprinting ? sprintBobSpeed : walkBobSpeed);
            currentAmount = (crouching ? crouchBobAmount : sprinting ? sprintBobAmount : walkBobAmount);

            headbobOffset = new Vector3(cam.localPosition.x, cam.localPosition.y + Mathf.Sin(currentSpeed) * currentAmount, cam.localPosition.z);

            cam.localPosition = Vector3.Lerp(cam.localPosition, headbobOffset, headbobSmooth * Time.deltaTime);
        }
    }
    #endregion
}