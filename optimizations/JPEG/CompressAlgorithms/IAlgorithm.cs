namespace JPEG;

public interface IAlgorithm
{
    void Forward(float[,] input, float[,] result);
    void Backward(float[,] coeffs, float[,] output);
}