﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Extension.BaseService;
using Grpc.Extension.Interceptors;
using Grpc.Extension.Model;

namespace Grpc.Extension.Internal
{
    public class ServerBuilder
    {
        private readonly List<Interceptor> _interceptors = new List<Interceptor>();
        private readonly List<ServerServiceDefinition> _serviceDefinitions = new List<ServerServiceDefinition>();

        /// <summary>
        /// 注入Grpc配制
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public ServerBuilder UseGrpcOptions(GrpcServerOptions options)
        {
            GrpcServerOptions.Instance.ServiceAddress = options.ServiceAddress;
            GrpcServerOptions.Instance.ConsulUrl = options.ConsulUrl;
            GrpcServerOptions.Instance.ConsulServiceName = options.ConsulServiceName;
            GrpcServerOptions.Instance.ConsulTags = options.ConsulTags;
            return this;
        }

        /// <summary>
        /// 注入GrpcService
        /// </summary>
        /// <param name="serviceDefinition"></param>
        /// <returns></returns>
        public ServerBuilder UseGrpcService(ServerServiceDefinition serviceDefinition)
        {
            _serviceDefinitions.Add(serviceDefinition);
            return this;
        }

        /// <summary>
        /// 注入GrpcService
        /// </summary>
        /// <param name="grpcServices"></param>
        /// <returns></returns>
        public ServerBuilder UseGrpcService(IEnumerable<IGrpcService> grpcServices)
        {
            var builder = ServerServiceDefinition.CreateBuilder();
            grpcServices.ToList().ForEach(grpc => grpc.RegisterMethod(builder));
            _serviceDefinitions.Add(builder.Build());
            return this;
        }

        public ServerBuilder UseBaseInterceptor()
        {
            //性能监控，熔断处理
            this.UseInterceptor(new MonitorInterceptor())//性能监控
                .UseInterceptor(new ThrottleInterceptor());//熔断处理
            return this;
        }

        /// <summary>
        /// 注入中间件
        /// </summary>
        /// <param name="interceptor"></param>
        /// <returns></returns>
        public ServerBuilder UseInterceptor(Interceptor interceptor)
        {
            _interceptors.Add(interceptor);
            return this;
        }

        public Server Build()
        {
            Server server = new Server();
            //使用拦截器
            var serviceDefinitions = ApplyInterceptor(_serviceDefinitions, _interceptors);
            //添加服务定义
            foreach (var serviceDefinition in serviceDefinitions)
            {
                server.Services.Add(serviceDefinition);
            }
            //添加服务IPAndPort
            var ipPort = NetHelper.GetIPAndPort(GrpcServerOptions.Instance.ServiceAddress);
            server.Ports.Add(new ServerPort(ipPort.Item1, ipPort.Item2, ServerCredentials.Insecure));
            
            return server;
        }

        private static IEnumerable<ServerServiceDefinition> ApplyInterceptor(IEnumerable<ServerServiceDefinition> serviceDefinitions, IEnumerable<Interceptor> interceptors)
        {
            return serviceDefinitions.Select(serviceDefinition => serviceDefinition.Intercept(interceptors.ToArray()));
        }
    }
}
