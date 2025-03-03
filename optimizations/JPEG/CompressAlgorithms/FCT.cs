using System;
using JPEG;

public class FCT : IAlgorithm
{
    private readonly float[,] DctMatrix;
    private readonly float BetaValue;
    private readonly byte DCTSize;
    private readonly float[,] Cos;
    private readonly float[] AlphaValues;
    private readonly float sqrt2n;
    private readonly float invSqrt2;

    public FCT(byte dctSize)
    {
        DCTSize = dctSize;
        Cos = new float[dctSize, dctSize];
        DctMatrix = new float[dctSize, dctSize];
        AlphaValues = new float[dctSize];
        BetaValue = 1f / dctSize + 1f / dctSize;
        sqrt2n = (float)Math.Sqrt(2.0 / dctSize);
        invSqrt2 = (float)(1.0 / Math.Sqrt(2));

        for (int u = 0; u < dctSize; u++)
        {
            AlphaValues[u] = u == 0 ? invSqrt2 : 1.0f;
        }

        for (int u = 0; u < dctSize; u++)
        {
            for (int v = 0; v < dctSize; v++)
            {
                Cos[u, v] = (float)Math.Cos((2 * v + 1) * u * Math.PI / (2 * dctSize));
            }
        }

        for (int u = 0; u < dctSize; u++)
        {
            for (int v = 0; v < dctSize; v++)
            {
                DctMatrix[u, v] = BetaValue * AlphaValues[u] * AlphaValues[v];
            }
        }
    }

    public void Forward(float[,] input, float[,] result)
    {
        float[] temp = new float[DCTSize];

        for (int i = 0; i < DCTSize; i++)
        {
            for (int j = 0; j < DCTSize; j++)
            {
                temp[j] = input[i, j];
            }

            FCTMove(temp);
            for (int j = 0; j < DCTSize; j++)
            {
                result[i, j] = temp[j];
            }
        }

        for (int j = 0; j < DCTSize; j++)
        {
            for (int i = 0; i < DCTSize; i++)
            {
                temp[i] = result[i, j];
            }

            FCTMove(temp);
            for (int i = 0; i < DCTSize; i++)
            {
                result[i, j] = temp[i];
            }
        }
    }

    public void Backward(float[,] coeffs, float[,] output)
    {
        float[] temp = new float[DCTSize];

        for (int i = 0; i < DCTSize; i++)
        {
            for (int j = 0; j < DCTSize; j++)
            {
                temp[j] = coeffs[i, j];
            }

            IFCTMove(temp);
            for (int j = 0; j < DCTSize; j++)
            {
                output[i, j] = temp[j];
            }
        }

        for (int j = 0; j < DCTSize; j++)
        {
            for (int i = 0; i < DCTSize; i++)
            {
                temp[i] = output[i, j];
            }

            IFCTMove(temp);
            for (int i = 0; i < DCTSize; i++)
            {
                output[i, j] = temp[i];
            }
        }
    }

    private void FCTMove(float[] data)
    {
        float[] temp = new float[DCTSize];

        for (int k = 0; k < DCTSize; k++)
        {
            float sum = 0;
            for (int i = 0; i < DCTSize; i++)
            {
                sum += data[i] * Cos[k, i];
            }

            temp[k] = sum * sqrt2n;
        }

        temp[0] *= invSqrt2;

        Array.Copy(temp, data, DCTSize);
    }

    private void IFCTMove(float[] data)
    {
        float[] temp = new float[DCTSize];

        for (int i = 0; i < DCTSize; i++)
        {
            float sum = 0;
            for (int k = 0; k < DCTSize; k++)
            {
                float alphaK = k == 0 ? invSqrt2 : 1.0f;
                sum += alphaK * data[k] * Cos[k, i];
            }

            temp[i] = sum * sqrt2n;
        }

        Array.Copy(temp, data, DCTSize);
    }
}