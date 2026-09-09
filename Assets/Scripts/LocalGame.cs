using UnityEngine;
using UnityEngine.InputSystem;

public class LocalGame : MonoBehaviour
{
    [field: SerializeField] GameManager m_gameManager;
    [field: SerializeField] InputAction m_ballDirection;

    private void OnEnable()
    {
        m_ballDirection.Enable();
    }

    private void OnDisable()
    {
        m_ballDirection.Disable();
    }

    void Start()
    {
        m_gameManager.AddCoins(); // populate coins
        m_gameManager.AddBall(0); // add local player
      
    }

    private void FixedUpdate()
    {
        BallInput input = new BallInput();
        input.direction = m_ballDirection.ReadValue<Vector2>();
        m_gameManager.SetBallInput(0, input);
        m_gameManager.Step(Time.fixedDeltaTime);
        if(m_gameManager.GetCoinCount() == 0)
        {
            Debug.Log("You've collected all coins!");
            Destroy(m_gameManager.gameObject);
            Destroy(gameObject);
        }
    }
}
