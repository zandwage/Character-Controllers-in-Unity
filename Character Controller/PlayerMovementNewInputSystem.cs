using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform cam;
    private CharacterController controller;
    private PlayerInput playerInput;

    [Header("Functional Options")]
    public bool canMove;
    public bool canLook;

    [Header("Look Settings")]
    public float sensitivity = 2f;

    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 7f;
    public float crouchSpeed = 3f;
    public float moveSmoothTime;
    public float crouchSmoothTime;
    public float normalGravityStrength;
    public float jumpGravityStrength;
    public float jumpForce;
    public float pushForce;
    public float landThreshold;

    [Header("Headbob Settings")]
    public float headbobSmooth;
    public float walkBobSpeed, walkBobAmount;
    public float sprintBobSpeed, sprintBobAmount;
    public float crouchBobSpeed, crouchBobAmount;

    [Header("Debugging")]
    public float fallTimer;
    public bool grounded;
    public bool groundedLastFrame;
    public bool justLanded;
    public bool moving;
    public bool sprinting;
    public bool crouching;
    public bool crouchingUnderSomething;
    public bool jumping;   

    private float originalStepOffset;
    private float gravityStrength;

    private float moveSpeed;   

    private float xRotation, yRotation; 

    private float standHeight, crouchHeight;
    private float standCenter, crouchCenter;
    private float standCamPos, crouchCamPos;
    private float currentHeight;
    private Vector3 currentCenter;
    private Vector3 currentCamPos;

    private Vector3 moveDirection;
    private Vector3 input;    

    private Vector2 moveInput;
    private Vector2 lookInput;

    private Vector3 currentMoveVelocity;
    private Vector3 currentForceVelocity;   

    private bool walkingForwards; 

    private float currentSpeed, currentAmount;
    private Vector3 headbobPos;

    void Start()
    {
        canLook = true;
        canMove = true;

        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        standHeight = controller.height;
        crouchHeight = standHeight * 0.55f;
        standCenter = controller.center.y;
        crouchCenter = standCenter * 0.55f;
        standCamPos = cam.transform.localPosition.y;
        crouchCamPos = standCamPos - 0.85f;

        originalStepOffset = 0.4f;
        gravityStrength = normalGravityStrength;

        controller.stepOffset = originalStepOffset;    
        controller.minMoveDistance = 0.0001f;
        controller.skinWidth = 0.0001f;
        controller.center = new Vector3(0, 1, 0);    
    }

    void Update()
    {
        grounded = controller.isGrounded;

        Cursor.lockState = canLook ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = canLook ? false : true;        

        if (canMove)
        {
            CheckForObjects();      
            CheckCrouchingUnderSomething();
            SetLandBool();

            HandleInput();
            HandleGravity();
            HandleHeadbobbing();

            HandleMovement();             
        }
    }

    void FixedUpdate()
    {
        if (canMove)
            SetFallTimer();           
    }

    void LateUpdate()
    {
        if (canLook)
            HandleLooking();   
    }

    void SetFallTimer()
    {
        fallTimer = (grounded ? 0f : fallTimer += Time.deltaTime);
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

    void HandleLooking()
    {
        lookInput = playerInput.lookAction.action.ReadValue<Vector2>();

        xRotation -= lookInput.y * sensitivity * 0.05f;
        yRotation += lookInput.x * sensitivity * 0.05f;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cam.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.localRotation = Quaternion.Euler(0, yRotation, 0);            
    }

    void HandleInput()
    {
        moveInput = playerInput.moveAction.action.ReadValue<Vector2>();

        input = new Vector3(moveInput.x, 0f, moveInput.y);    
        moveDirection = transform.TransformDirection(input);
        
        currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, moveDirection.normalized * moveSpeed, moveSmoothTime * Time.deltaTime);

        moving = Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z)> 0.1f;        
        walkingForwards = input.z > 0f;        

        jumping = playerInput.jumpAction.action.WasPressedThisFrame();
        sprinting = playerInput.sprintAction.action.IsPressed() && walkingForwards;
        crouching = playerInput.crouchAction.action.IsPressed() || crouchingUnderSomething;

        moveSpeed = crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;

        HandleJumping(jumping);
        HandleCrouching(crouching);
    }

    void HandleGravity()
    {
        currentForceVelocity.y -= gravityStrength * Time.deltaTime;

        if (grounded && currentForceVelocity.y < 0f)
            currentForceVelocity.y = -8f;       
    }

    void HandleMovement()
    {
        controller.Move(currentMoveVelocity * Time.deltaTime);
        controller.Move(currentForceVelocity * Time.deltaTime);        
    }

    void HandleJumping(bool jumping)
    {
        if (grounded && jumping)
        {
            gravityStrength = jumpGravityStrength;
            currentForceVelocity.y = jumpForce;
        }
        else if (grounded && !jumping)
        {
            gravityStrength = normalGravityStrength;
        }
    }
    
    void HandleCrouching(bool crouching)
    {
        currentCenter = new Vector3(controller.center.x, crouching ? crouchCenter : standCenter, controller.center.z);
        currentCamPos = new Vector3(cam.localPosition.x, crouching ? crouchCamPos : standCamPos, cam.localPosition.z);
        currentHeight = crouching ? crouchHeight : standHeight;

        cam.localPosition = Vector3.Lerp(cam.localPosition, currentCamPos, crouchSmoothTime * Time.deltaTime);
        controller.center = Vector3.Lerp(controller.center, currentCenter, crouchSmoothTime * Time.deltaTime);
        controller.height = Mathf.Lerp(controller.height, currentHeight, crouchSmoothTime * Time.deltaTime);        
    }

    void HandleHeadbobbing()
    {
        if (moving && grounded)
        {
            currentSpeed += Time.deltaTime * (crouching ? crouchBobSpeed : sprinting ? sprintBobSpeed : walkBobSpeed);
            currentAmount = crouching ? crouchBobAmount : sprinting ? sprintBobAmount : walkBobAmount;

            headbobPos = new Vector3(cam.localPosition.x, cam.localPosition.y + Mathf.Sin(currentSpeed) * currentAmount, cam.localPosition.z);

            cam.localPosition = Vector3.Lerp(cam.localPosition, headbobPos, headbobSmooth * Time.deltaTime);
        }
    }
    
    void CheckForObjects()
    {
        bool collision = Physics.SphereCast(transform.position, controller.radius + 0.2f, transform.forward, out RaycastHit hit, controller.radius + 0.2f);

        if (collision)
        {
            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)
                controller.stepOffset = 0.01f;
            else
                controller.stepOffset = originalStepOffset;
        }
        else
        {
            controller.stepOffset = originalStepOffset;
        }
    }

    void CheckCrouchingUnderSomething()
    {
        crouchingUnderSomething = Physics.Raycast(cam.transform.position, Vector3.up, 0.5f);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.collider.attachedRigidbody;

        if (rb != null && rb && !rb.isKinematic && hit.moveDirection.y > -0.3f)
        {
            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            rb.velocity = pushDir * pushForce;
        }
    }
}
