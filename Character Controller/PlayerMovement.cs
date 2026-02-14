using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public Transform cam;
    private CharacterController controller;

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
    private Vector3 playerInput;

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

        standHeight = controller.height;
        crouchHeight = standHeight * 0.55f;
        standCenter = controller.center.y;
        crouchCenter = standCenter * 0.55f;
        standCamPos = cam.transform.localPosition.y;
        crouchCamPos = standCamPos - 0.85f;

        originalStepOffset = controller.stepOffset;
        gravityStrength = normalGravityStrength;
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
        xRotation -= Input.GetAxisRaw("Mouse Y") * sensitivity;
        yRotation += Input.GetAxisRaw("Mouse X") * sensitivity;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cam.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.localRotation = Quaternion.Euler(0, yRotation, 0);            
    }

    void HandleInput()
    {
        playerInput = new Vector3(
            Input.GetKey(PlayerInput.right) ? 1f : Input.GetKey(PlayerInput.left) ? -1f : 0f,
            0f,
            Input.GetKey(PlayerInput.forward) ? 1f : Input.GetKey(PlayerInput.backward) ? -1f : 0f
        );

        moveDirection = transform.TransformDirection(playerInput);
        
        currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, moveDirection.normalized * moveSpeed, moveSmoothTime * Time.deltaTime);

        moving = Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z)> 0.1f;        
        walkingForwards = playerInput.z > 0f;        

        jumping = Input.GetKeyDown(PlayerInput.jump);
        sprinting = Input.GetKey(PlayerInput.sprint) && walkingForwards;
        crouching = Input.GetKey(PlayerInput.crouch) || crouchingUnderSomething;

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
            rb.linearVelocity = pushDir * pushForce;
        }
    }
}
