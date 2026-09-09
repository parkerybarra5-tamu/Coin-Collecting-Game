using UnityEngine;

public class BallPhysics : MonoBehaviour
{
    [field: SerializeField] Rigidbody m_body;

    BallInput m_input;

    public void SetBallInput(BallInput input)
    {
        m_input = input;
    }

    public void Step(float dt)
    {
        m_body.linearVelocity = new Vector3(m_input.direction.x, 0, m_input.direction.y);
    }
    
}
