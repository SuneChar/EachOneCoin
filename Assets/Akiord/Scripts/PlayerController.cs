using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private float speed;
    private void Start()
    {
        if(player == null)
            player = GameObject.FindGameObjectWithTag("Player");
    }
    private void Update()
    {
        player.transform.position += 
            new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * Time.deltaTime * speed;
    }
}
