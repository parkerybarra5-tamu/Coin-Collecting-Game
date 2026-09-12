using UnityEngine;

public class BallPhysics : MonoBehaviour
{
    [field: SerializeField] public Rigidbody m_body { get; private set; }

    [Header("Source Movement Settings")]
    [SerializeField] float m_maxSpeed = 8f;        // Max ground speed
    [SerializeField] float m_maxAirSpeed = 0.8f;   // Max speed added via air control (keep low for Source air-strafing)
    [SerializeField] float m_acceleration = 10f;   // Ground acceleration multiplier
    [SerializeField] float m_airAcceleration = 10f;// Air acceleration multiplier

    [Header("Source Friction")]
    [SerializeField] float m_friction = 4f;        // Ground friction multiplier
    [SerializeField] float m_stopSpeed = 2f;       // Minimum speed before friction drops you to 0

    [Header("Jumping & Slopes")]
    [SerializeField] float m_jumpForce = 5f;
    [SerializeField] float m_groundCheckDistance = 0.55f;
    [SerializeField] LayerMask m_groundMask = ~0;

    BallInput m_input = new BallInput { direction = Vector3.zero, jump = false };
    bool m_isGrounded;
    Vector3 m_groundNormal = Vector3.up;

    public void SetBallInput(BallInput input)
    {
        m_input = input;
    }

    public void Step(float dt)
    { 
        CheckGround();

        // 1. Calculate Wish Direction and Wish Speed
        Vector3 wishDir = new Vector3(m_input.direction.x, 0, m_input.direction.y);
        float wishSpeed = wishDir.magnitude * m_maxSpeed;

        if (wishSpeed > 0f)
        {
            wishDir.Normalize();
        }

        Vector3 velocity = m_body.linearVelocity;

        if (m_isGrounded)
        {
            // Orient wish direction to the slope
            wishDir = ClipVelocity(wishDir, m_groundNormal).normalized;

            // Flatten velocity to the slope before calculations
            velocity = ClipVelocity(velocity, m_groundNormal);

            // 2. Apply Source Engine Friction
            velocity = ApplyFriction(velocity, dt);

            // 3. Apply Source Engine Ground Acceleration
            velocity = Accelerate(velocity, wishDir, wishSpeed, m_acceleration, dt);

            // Re-clip to ensure acceleration didn't push us into/off the slope
            velocity = ClipVelocity(velocity, m_groundNormal);

            // 4. Jump
            if (m_input.jump)
            {
                velocity.y = m_jumpForce; // Direct velocity override for Source-style crisp jumps
                m_isGrounded = false;
            }
        }
        else
        {
            // 5. Apply Source Engine Air Acceleration (No friction in air)
            // Limit the wishSpeed for air control to allow turning without infinite speed gain
            float airWishSpeed = Mathf.Min(wishSpeed, m_maxAirSpeed);
            velocity = Accelerate(velocity, wishDir, airWishSpeed, m_airAcceleration, dt);
        }

        m_body.linearVelocity = velocity;
    }

    void CheckGround()
    {
        m_isGrounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, m_groundCheckDistance, m_groundMask);
        m_groundNormal = m_isGrounded ? hit.normal : Vector3.up;
    }

    // Source Engine: Accelerate based on the dot product of current velocity and wish direction
    Vector3 Accelerate(Vector3 currentVelocity, Vector3 wishDir, float wishSpeed, float accel, float dt)
    {
        // How much speed we already have in the direction we want to go
        float currentSpeed = Vector3.Dot(currentVelocity, wishDir);

        // How much more speed we need to reach our wishSpeed
        float addSpeed = wishSpeed - currentSpeed;

        // If we don't need to add any speed, return
        if (addSpeed <= 0) return currentVelocity;

        // Determine acceleration amount based on accel multiplier and delta time
        float accelSpeed = accel * dt * wishSpeed;

        // Clamp so we don't overshoot the wishSpeed
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        // Add the new speed vector
        return currentVelocity + (wishDir * accelSpeed);
    }

    // Source Engine: Apply friction using a stop speed to completely halt small movements
    Vector3 ApplyFriction(Vector3 currentVelocity, float dt)
    {
        float speed = currentVelocity.magnitude;
        if (speed < 0.01f) return Vector3.zero;

        // Use stopSpeed if moving very slowly, otherwise use current speed
        float control = (speed < m_stopSpeed) ? m_stopSpeed : speed;

        // Calculate velocity drop
        float drop = control * m_friction * dt;

        // Calculate new speed
        float newSpeed = Mathf.Max(speed - drop, 0);
        newSpeed /= speed;

        return currentVelocity * newSpeed;
    }

    // Source Engine: Project vector onto a plane (slides along slopes)
    Vector3 ClipVelocity(Vector3 input, Vector3 normal)
    {
        float backoff = Vector3.Dot(input, normal);
        return input - normal * backoff;
    }
}