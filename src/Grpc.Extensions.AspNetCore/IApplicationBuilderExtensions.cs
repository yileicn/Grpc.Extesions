﻿using Grpc.Extension;
using Grpc.Extension.Abstract;
using Grpc.Extension.AspNetCore;
using Grpc.Extension.BaseService;
using Grpc.Extension.Common.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTracing;
using OpenTracing.Util;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Grpc.Extensions.AspNetCore
{
    public static class IApplicationBuilderExtensions
    {
        /// <summary>
        /// 使用Grpc扩展
        /// </summary>
        /// <typeparam name="TStartup">实现IGrpcService的类所在程序集下的任意类</typeparam>
        /// <param name="builder"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseGrpcExtensions<TStartup>(this IApplicationBuilder builder, Action<GrpcExtensionsOptions> configureOptions = null)
        {
            ServiceProviderAccessor.SetServiceProvider(builder.ApplicationServices);
            //注入基本配制
            configureOptions?.Invoke(GrpcExtensionsOptions.Instance);
            //使用基础服务
            builder.UseEndpoints(endpoints => endpoints.MapIGrpcServices<TStartup>());
            //默认使用
            builder.UseLoggerFactory()//使用LoggerFactory
                .UseJaeger();//使用Jaeger

            return builder;
        }

        /// <summary>
        /// MapGrpcService所有的IGrpcService和基础服务
        /// </summary>
        /// <typeparam name="TStartup"></typeparam>
        /// <param name="endpoints"></param>
        private static void MapIGrpcServices<TStartup>(this IEndpointRouteBuilder endpoints)
        {
            //获取所有IGrpcService并调用MapGrpcService方法
            var grpcServices = typeof(TStartup).Assembly.GetTypes().Where(p => typeof(IGrpcService).IsAssignableFrom(p) && p.IsClass);
            var method = typeof(GrpcEndpointRouteBuilderExtensions).GetMethod("MapGrpcService");
            foreach (var service in grpcServices)
            {
                var mapGrpcService = method.MakeGenericMethod(service);
                mapGrpcService.Invoke(null, new object[] { endpoints });
            }
            //使用基础服务
            endpoints.MapGrpcService<CmdService>();
            endpoints.MapGrpcService<MetaService>();
        }

        /// <summary>
        /// 注入Grpc,Discovery配制
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseGrpcOptions(this IApplicationBuilder builder, Action<GrpcServerOptions> configureOptions)
        {
            var options = ServiceProviderAccessor.GetService<IOptions<GrpcServerOptions>>().Value;
            configureOptions(options);

            return builder;
        }

        /// <summary>
        /// 有Jaeger配制就使用Jaeger
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        private static IApplicationBuilder UseJaeger(this IApplicationBuilder builder)
        {
            var serverOptions = ServiceProviderAccessor.GetService<IOptions<GrpcServerOptions>>()?.Value;
            if (serverOptions.Jaeger != null && string.IsNullOrWhiteSpace(serverOptions.Jaeger.ServiceName))
                serverOptions.Jaeger.ServiceName = serverOptions.DiscoveryServiceName;

            if (serverOptions.Jaeger?.CheckConfig() == true)
            {
                var tracer = ServiceProviderAccessor.GetService<ITracer>();
                if (tracer != null) GlobalTracer.Register(tracer);
            }
            return builder;
        }

        /// <summary>
        /// 使用LoggerFactory
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        private static IApplicationBuilder UseLoggerFactory(this IApplicationBuilder builder)
        {
            var loggerFactory = ServiceProviderAccessor.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<IApplicationBuilder>();
            var loggerAccess = loggerFactory.CreateLogger("grpc.access");

            LoggerAccessor.Instance.LoggerError += (ex, type) => logger.LogError(ex.ToString());
            LoggerAccessor.Instance.LoggerMonitor += (msg, type) => loggerAccess.LogInformation(msg);

            return builder;
        }

        /// <summary>
        /// 配制日志(默认使用LoggerFactory)
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configureLogger"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseLogger(this IApplicationBuilder builder, Action<LoggerAccessor> configureLogger)
        {
            configureLogger(LoggerAccessor.Instance);

            return builder;
        }

        /// <summary>
        /// CodeFirst生成proto文件
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="dir">生成目录</param>
        /// <param name="spiltProto">是否拆分service和message协议</param>
        /// <returns></returns>
        public static IApplicationBuilder UseProtoGenerate(this IApplicationBuilder builder, string dir, bool spiltProto = true)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                ProtoGenerator.Gen(dir, spiltProto);

            return builder;
        }
    }
}
