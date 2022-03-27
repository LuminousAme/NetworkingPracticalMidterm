using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cube : MonoBehaviour
{
    [SerializeField] int cubeId = 0;
    public int GetCubeId() => cubeId;

    [SerializeField] float timeBetweenSends = 0.05f;
    float elapsedTime = 0f;

    // Start is called before the first frame update
    void Start()
    {
        elapsedTime = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        if ((Client.instance != null && Client.instance.GetClientId() == cubeId) || Client.instance == null )
        {
            transform.Translate(Input.GetAxis("Horizontal") * Time.deltaTime * 2f, 
                0, Input.GetAxis("Vertical") * Time.deltaTime * 2f);
        }

        if(Client.instance != null && Client.instance.GetClientId() == cubeId)
        {
            elapsedTime += Time.deltaTime;
            if(elapsedTime >= timeBetweenSends)
            {
                Client.instance.SendPosUpdate(transform.position);
                elapsedTime = 0f;
            } 
        }
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
        Debug.Log("Pos Recieved: " + pos);
    }
}
