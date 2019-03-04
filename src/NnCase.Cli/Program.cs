﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics.Tensors;
using System.Threading.Tasks;
using CommandLine;
using NnCase.Converter.Converters;
using NnCase.Converter.Data;
using NnCase.Converter.Model;
using NnCase.Converter.Model.Layers;
using NnCase.Converter.Model.Layers.K210;
using NnCase.Converter.Transforms;
using NnCase.Converter.Transforms.K210;

namespace NnCase.Cli
{
    public class Options
    {
        [Option('i', "input-format", Required = true, HelpText = "Set the input format.")]
        public string InputFormat { get; set; }

        [Option('o', "output-format", Required = true, HelpText = "Set the input format.")]
        public string OutputFormat { get; set; }

        [Option("input-node", Required = false, HelpText = "Input node")]
        public string InputNode { get; set; }

        [Option("output-node", Required = false, HelpText = "Output node")]
        public string OutputNode { get; set; }

        [Option("dataset", Required = false, HelpText = "Dataset path")]
        public string Dataset { get; set; }

        [Option("postprocess", Required = false, HelpText = "Dataset postprocess")]
        public string Postprocess { get; set; }

        [Option("weights-bits", Required = false, HelpText = "Weights quantization bits", Default = 8)]
        public int WeightsBits { get; set; }

        [Value(0, MetaName = "input", HelpText = "Input path")]
        public string Input { get; set; }

        [Value(1, MetaName = "output", HelpText = "Output path")]
        public string Output { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Console.WriteLine("Fatal: " + ex.Message);
                else
                    Console.WriteLine("Fatal: Unexpected error occurred.");
                Environment.Exit(-1);
            };

            Options options = null;
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o => options = o);
            if (options == null) return;

            Graph graph;
            switch (options.InputFormat.ToLowerInvariant())
            {
                case "caffe":
                    {
                        var file = File.ReadAllBytes(options.Input);
                        var model = Caffe.NetParameter.Parser.ParseFrom(file);
                        var tfc = new CaffeToGraphConverter(model);
                        tfc.Convert();
                        graph = tfc.Graph;
                        break;
                    }
                case "paddle":
                    {
                        var tfc = new PaddleToGraphConverter(options.Input);
                        tfc.Convert(0);
                        graph = tfc.Graph;
                        break;
                    }
                case "tflite":
                    {
                        var file = File.ReadAllBytes(options.Input);
                        var model = tflite.Model.GetRootAsModel(new FlatBuffers.ByteBuffer(file));
                        var tfc = new TfLiteToGraphConverter(model, model.Subgraphs(0).Value);
                        tfc.Convert();
                        graph = tfc.Graph;
                        break;
                    }
                case "test":
                    {
                        var inputs = new[]
                        {
                            new InputLayer(new[]{-1,3,8,8}){ Name ="input" }
                        };
                        var conv2d = new K210Conv2d(inputs[0].Output.Dimensions, K210Conv2dType.Conv2d,
                            new DenseTensor<float>(new[] { 32, 3, 3, 3 }), null, K210PoolType.None, ActivationFunctionType.Relu);
                        conv2d.Input.SetConnection(inputs[0].Output);

                        var spconv2d = new K210SeparableConv2d(conv2d.Output.Dimensions, new DenseTensor<float>(new[] { 1, 32, 3, 3, 3 }),
                            new DenseTensor<float>(new[] { 32, 64, 1, 1 }), null, K210PoolType.LeftTop, ActivationFunctionType.Relu);
                        spconv2d.Input.SetConnection(conv2d.Output);

                        var outputs = new[]
                        {
                            new OutputLayer(spconv2d.Output.Dimensions){Name = "output"}
                        };
                        outputs[0].Input.SetConnection(spconv2d.Output);
                        graph = new Graph(inputs, outputs);
                    }
                    break;
                default:
                    throw new ArgumentException("input-format");
            }

            var outputFormat = options.OutputFormat.ToLowerInvariant();
            switch (outputFormat)
            {
                case "tf":
                    {
                        var ctx = new GraphPlanContext();
                        graph.Plan(ctx);

                        using (var f = File.Open(options.Output, FileMode.Create, FileAccess.Write))
                            await ctx.SaveAsync(f);
                        break;
                    }
                case "tflite":
                    {
                        await ConvertToTFLite(graph, options.Output);
                        break;
                    }
                case "k210model":
                case "k210pb":
                    {
                        PostprocessMethods pm = PostprocessMethods.Normalize0To1;
                        if (options.Postprocess == "n1to1")
                            pm = PostprocessMethods.NormalizeMinus1To1;

                        if (options.InputFormat.ToLowerInvariant() != "tflite")
                        {
                            var tmpTflite = Path.GetTempFileName();
                            await ConvertToTFLite(graph, tmpTflite);

                            var file = File.ReadAllBytes(tmpTflite);
                            File.Delete(tmpTflite);
                            var model = tflite.Model.GetRootAsModel(new FlatBuffers.ByteBuffer(file));
                            var tfc = new TfLiteToGraphConverter(model, model.Subgraphs(0).Value);
                            tfc.Convert();
                            graph = tfc.Graph;
                        }

                        Transform.Process(graph, new Transform[] {
                            new K210SeparableConv2dTransform(),
                            new K210SpaceToBatchNdAndValidConv2dTransform(),
                            new K210SameConv2dTransform(),
                            new K210Stride2Conv2dTransform(),
                            new GlobalAveragePoolTransform(),
                            new K210FullyConnectedTransform(),
                            new K210Conv2dWithMaxAvgPoolTransform(),
                            new Conv2d1x1ToFullyConnectedTransform(),
                            new K210EliminateAddRemovePaddingTransform(),
                            new EliminateQuantizeDequantizeTransform(),
                            new EliminateInputQuantizeTransform(),
                            //new EliminateDequantizeOutputTransform()
                        });

                        {
                            var ctx = new GraphPlanContext();
                            graph.Plan(ctx);
                            if (outputFormat == "k210model")
                            {
                                var dim = graph.Inputs.First().Output.Dimensions.ToArray();
                                var k210c = new GraphToK210Converter(graph, options.WeightsBits);
                                await k210c.ConvertAsync(new ImageDataset(
                                    options.Dataset,
                                    new[] { dim[1], dim[2], dim[3] },
                                    1,
                                    PreprocessMethods.None,
                                    pm),
                                    ctx,
                                    Path.GetDirectoryName(options.Output),
                                    Path.GetFileNameWithoutExtension(options.Output));
                            }
                            else
                            {
                                using (var f = File.Open(options.Output, FileMode.Create, FileAccess.Write))
                                    await ctx.SaveAsync(f);
                            }
                        }
                        break;
                    }
                case "k210script":
                    {
                        {
                            var dim = graph.Inputs.First().Output.Dimensions.ToArray();
                            var k210c = new GraphToScriptConverter(graph);
                            await k210c.ConvertAsync(
                                Path.GetDirectoryName(options.Output),
                                Path.GetFileNameWithoutExtension(options.Output));
                        }
                        break;
                    }
                default:
                    throw new ArgumentException("output-format");
            }
        }

        private static async Task ConvertToTFLite(Graph graph, string tflitePath)
        {
            var ctx = new GraphPlanContext();
            graph.Plan(ctx);
            var dim = graph.Inputs.First().Output.Dimensions.ToArray();
            var input = graph.Inputs.First().Name;
            var output = graph.Outputs.First().Name;

            var tmpPb = Path.GetTempFileName();
            using (var f = File.Open(tmpPb, FileMode.Create, FileAccess.Write))
                await ctx.SaveAsync(f);

            var binPath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "bin");
            var args = $" --input_file={tmpPb} --input_format=TENSORFLOW_GRAPHDEF --output_file={tflitePath} --output_format=TFLITE --input_shape=1,{dim[2]},{dim[3]},{dim[1]} --input_array={input} --output_array={output} --inference_type=FLOAT";
            using (var toco = Process.Start(new ProcessStartInfo(Path.Combine(binPath, "toco"), args)
            {
                WorkingDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)
            }))
            {
                toco.WaitForExit();
                if (toco.ExitCode != 0)
                    throw new InvalidOperationException("Convert to tflite failed.");
            }
            File.Delete(tmpPb);
        }
    }
}
