﻿using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using AWSLambda.Internal.Bootstrap;
using AWSLambda.Internal.Bootstrap.Context;

namespace MockLambdaRuntime
{
    class Program
    {
        /// <summary>
        /// Task root of lambda task
        /// </summary>
        private static string lambdaTaskRoot;

        /// <summary>
        /// Entry point
        /// </summary>
        static void Main(string[] args)
        {
            AssemblyLoadContext.Default.Resolving += OnAssemblyResolving;
            lambdaTaskRoot = GetEnvironmentVariable("LAMBDA_TASK_ROOT", "/var/task");

            string handler = GetFunctionHandler(args);
            string body = GetContext(args);

            var lambdaContext = new MockLambdaContext(body, Environment.GetEnvironmentVariables());

            var userCodeLoader = new UserCodeLoader(handler, InternalLogger.NO_OP_LOGGER);
            userCodeLoader.Init(x => Console.WriteLine(x));

            var lambdaContextInternal = new LambdaContextInternal(lambdaContext.RemainingTime,
                                                                  LogAction, new Lazy<CognitoClientContextInternal>(),
                                                                  lambdaContext.RequestId,
                                                                  new Lazy<string>(lambdaContext.Arn),
                                                                  new Lazy<string>(string.Empty),
                                                                  new Lazy<string>(string.Empty),
                                                                  Environment.GetEnvironmentVariables());

            Exception lambdaException = null;

            LogStartRequest(lambdaContext);
            try
            {
                userCodeLoader.Invoke(lambdaContext.InputStream, lambdaContext.OutputStream, lambdaContextInternal);
            }
            catch (Exception ex)
            {
                lambdaException = ex;
            }
            LogEndRequest(lambdaContext);

            if (lambdaException == null)
            {
                Console.WriteLine(lambdaContext.OutputText);
            }
            else
            {
                Console.Error.WriteLine(lambdaException);
            }
        }

        /// <summary>
        /// Called when an assembly could not be resolved
        /// </summary>
        private static Assembly OnAssemblyResolving(AssemblyLoadContext context, AssemblyName assembly)
        {
            return context.LoadFromAssemblyPath(Path.Combine(lambdaTaskRoot, assembly.Name) + ".dll");
        }

        /// <summary>
        /// Logs the given text
        /// </summary>
        private static void LogAction(string text)
        {
            Console.Error.WriteLine(text);
        }

        /// <summary>
        /// Logs the start request.
        /// </summary>
        static void LogStartRequest(MockLambdaContext context)
        {
            Console.Error.WriteLine($"START RequestId: {context.RequestId} Version: {context.FunctionVersion}");
        }

        /// <summary>
        /// Logs the end request.
        /// </summary>
        static void LogEndRequest(MockLambdaContext context)
        {
            Console.Error.WriteLine($"END  RequestId: {context.RequestId}");

            Console.Error.WriteLine($"REPORT RequestId {context.RequestId}\t" +
                                    $"Duration: {context.Duration} ms\t" +
                                    $"Billed Duration: {context.BilledDuration} ms\t" +
                                    $"Memory Size {context.MemorySize} MB\t" +
                                    $"Max Memory Used: {context.MemoryUsed / (1024 * 1024)} MB");
        }

        /// <summary>
        /// Gets the function handler from arguments or environment
        /// </summary>
        static string GetFunctionHandler(string[] args)
        {
            return args.Length > 0 ? args[0] : GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_HANDLER", string.Empty);
        }

        /// <summary>
        /// Gets the context from arguments or environment
        /// </summary>
        static string GetContext(string[] args)
        {
            return args.Length > 1 ? args[1] : GetEnvironmentVariable("AWS_LAMBDA_CONTEXT", "{}");
        }


        /// <summary>
        /// Gets the environment variable.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="fallback">The fallback.</param>
        /// <returns></returns>
        static string GetEnvironmentVariable(string name, string fallback)
        {
            return Environment.GetEnvironmentVariable(name) ?? fallback;
        }

    }
}
