using UnityEngine;

public class CoinPhysics : MonoBehaviour
{
    GameManager gameManager;

    public CoinPhysics SetGameManager(GameManager gm)
    {
        Debug.Log("Manager Set");
        gameManager = gm;
        return this;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(gameManager == null)
        {
            Debug.LogWarning("Manger not set, make sure to call SetManager(GameManager)!");
            return;
        }
        BallPhysics ball;
        if (collision.gameObject.TryGetComponent<BallPhysics>(out ball))
        {
            gameManager.RemoveCoin(this, ball);
        }
    }
}
