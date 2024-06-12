using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class TriangularArrayTest : MonoBehaviour
{
    public GameObject sphere;
    void Start()
    {
        int2 CalculateXY(int total, int index)
        {
            int X;

            int rowValuesSum = 0;
            for (X = 0; X < total; X++)
            {
                rowValuesSum += total - 1 - X;
                if (index < rowValuesSum)
                {
                    break;
                }
            }

            int Y = index - rowValuesSum + total;

            return new int2(X, Y);
        }

        const int n = 16;
        int total = n * (n - 1) / 2;
        List<Transform> spheres = new();
        for (int index = 0; index < total; index++)
        {
            int2 result = CalculateXY(n, index);
            spheres.Add(GameObject.Instantiate(sphere).transform);
            spheres[^1].position = new Vector3(result.x, result.y, 0);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
