// Add this to a new file: Assets/Scripts/Brain.cs
using System;

public class Brain
{
    private int[] layers;
    private float[][] neurons;
    private float[][][] weights;
    private static System.Random rand = new System.Random();

    public Brain(int[] lay)
    {
        layers = (int[])lay.Clone();
        InitNeurons();
        InitWeights();
    }

    // Deep copy constructor
    public Brain(Brain other)
    {
        layers = (int[])other.layers.Clone();
        InitNeurons();
        InitWeights();
        // Copy weights
        for (int i = 0; i < weights.Length; i++)
            for (int j = 0; j < weights[i].Length; j++)
                for (int k = 0; k < weights[i][j].Length; k++)
                    weights[i][j][k] = other.weights[i][j][k];
    }

    private void InitNeurons()
    {
        neurons = new float[layers.Length][];
        for (int i = 0; i < layers.Length; i++)
            neurons[i] = new float[layers[i]];
    }

    private void InitWeights()
    {
        weights = new float[layers.Length - 1][][];
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = new float[neurons[i + 1].Length][];
            for (int j = 0; j < neurons[i + 1].Length; j++)
            {
                weights[i][j] = new float[neurons[i].Length];
                for (int k = 0; k < neurons[i].Length; k++)
                    weights[i][j][k] = (float)(rand.NextDouble() * 2 - 1); // random weights [-1,1]
            }
        }
    }

    // Feedforward: input -> output
    public float[] feedforward(float[] inputs)
    {
        Array.Copy(inputs, neurons[0], Math.Min(inputs.Length, neurons[0].Length));
        for (int layer = 1; layer < layers.Length; layer++)
        {
            for (int neuron = 0; neuron < neurons[layer].Length; neuron++)
            {
                float value = 0f;
                for (int prev = 0; prev < neurons[layer - 1].Length; prev++)
                    value += weights[layer - 1][neuron][prev] * neurons[layer - 1][prev];
                neurons[layer][neuron] = (float)Math.Tanh(value); // activation
            }
        }
        return neurons[neurons.Length - 1];
    }

    // Mutate weights slightly
    public void Mutate(float rate = 0.1f, float amount = 0.2f)
    {
        for (int i = 0; i < weights.Length; i++)
            for (int j = 0; j < weights[i].Length; j++)
                for (int k = 0; k < weights[i][j].Length; k++)
                    if (rand.NextDouble() < rate)
                        weights[i][j][k] += (float)(rand.NextDouble() * 2 - 1) * amount;
    }
}