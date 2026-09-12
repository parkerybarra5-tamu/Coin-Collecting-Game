using UnityEngine;
using UnityEngine.InputSystem;

public class LocalGame : MonoBehaviour
{
    [SerializeField] GameManager m_gameManager;
    [SerializeField] InputAction m_ballDirection;
    [SerializeField] InputAction m_ballJump;
    [SerializeField] SimpleThirdPersonCamera m_cam;

    [Header("Visuals")]
    [SerializeField] private GameObject m_ballVisualPrefab;
    public float m_ballRadius = 0.5f;

    private Transform m_ballVisual;

    // State tracking for smooth interpolation
    private Vector3 m_prevPos;
    private Vector3 m_currPos;
    private Quaternion m_prevRot = Quaternion.identity;
    private Quaternion m_currRot = Quaternion.identity;

    private void OnEnable()
    {
        m_ballDirection.Enable();
        m_ballJump.Enable();
    }

    private void OnDisable()
    {
        m_ballDirection.Disable();
        m_ballJump.Disable();
    }

    void Start()
    {
        m_gameManager.AddCoins();
        m_gameManager.AddBall(0);

        Transform ballTransform = m_gameManager.GetBallTransform(0);
        m_cam.SetCameraTarget(ballTransform);

        if (ballTransform != null)
        {
            if (m_ballVisualPrefab != null)
            {
                GameObject visualInst = Instantiate(m_ballVisualPrefab, ballTransform.position, Quaternion.identity);
                m_ballVisual = visualInst.transform;
            }

            // Initialize both prev and curr states to the starting position
            m_prevPos = ballTransform.position;
            m_currPos = ballTransform.position;
        }
    }

    private void FixedUpdate()
    {
        BallInput input = new BallInput();
        Vector2 k = m_ballDirection.ReadValue<Vector2>();
        input.direction = k.y * m_cam.GetFlatForward() + k.x * m_cam.GetFlatRight();
        input.jump = m_ballJump.IsPressed();

        m_gameManager.SetBallInput(0, input);
        m_gameManager.Step(Time.fixedDeltaTime);

        // Update physics tracking states
        Transform ballTransform = m_gameManager.GetBallTransform(0);
        if (ballTransform != null && m_ballVisual != null)
        {
            // Shift current state to previous state
            m_prevPos = m_currPos;
            m_prevRot = m_currRot;

            // Get new current state
            m_currPos = ballTransform.position;

            Vector3 displacement = m_currPos - m_prevPos;
            float distance = displacement.magnitude;

            if (distance > 0.0001f)
            {
                Vector3 moveDir = displacement / distance;
                Vector3 axis = Vector3.Cross(Vector3.up, moveDir).normalized;
                float angle = (distance / m_ballRadius) * Mathf.Rad2Deg;

                // Calculate the new target rotation based on the previous target
                m_currRot = Quaternion.AngleAxis(angle, axis) * m_prevRot;
            }
        }

        if (m_gameManager.GetCoinCount() == 0)
        {
            Debug.Log("You've collected all coins!");
            if (m_ballVisual != null) Destroy(m_ballVisual.gameObject);
            Destroy(m_gameManager.gameObject);
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (m_ballVisual != null)
        {
            // Calculate alpha: how far we are between the last FixedUpdate and the next one
            float alpha = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;

            // Clamp alpha between 0 and 1 to prevent visual overshooting
            alpha = Mathf.Clamp01(alpha);

            // Smoothly interpolate the visual position and rotation
            m_ballVisual.position = Vector3.Lerp(m_prevPos, m_currPos, alpha);
            m_ballVisual.rotation = Quaternion.Slerp(m_prevRot, m_currRot, alpha);
        }
    }
}