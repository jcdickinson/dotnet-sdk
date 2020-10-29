﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Http;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Autogenerated = Autogen.Grpc.v1;

    /// <summary>
    /// A client for interacting with the Dapr endpoints.
    /// </summary>
    internal class DaprClientGrpc : DaprClient
    {
        private static readonly string DaprErrorInfoHttpCodeMetadata = "http.code";
        private static readonly string DaprErrorInfoHttpErrorMetadata = "http.error_message";
        private static readonly string GrpcStatusDetails = "grpc-status-details-bin";
        private static readonly string GrpcErrorInfoDetail = "google.rpc.ErrorInfo";
        private static readonly string DaprHttpStatusHeader = "dapr-http-status";

        private readonly JsonSerializerOptions jsonSerializerOptions;
        private readonly Autogenerated.Dapr.DaprClient client;

        // property exposed for testing purposes
        internal Autogenerated.Dapr.DaprClient Client => client;

        // property exposed for testing purposes
        internal JsonSerializerOptions JsonSerializerOptions => jsonSerializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprClientGrpc"/> class.
        /// </summary>
        /// <param name="inner">Autogenerated.Dapr.DaprClient.</param>
        /// <param name="jsonSerializerOptions">Json serialization options.</param>
        internal DaprClientGrpc(Autogenerated.Dapr.DaprClient inner, JsonSerializerOptions jsonSerializerOptions)
        {
            this.client = inner;
            this.jsonSerializerOptions = jsonSerializerOptions;
        }

        #region Publish Apis
        /// <inheritdoc/>
        public override Task PublishEventAsync<TData>(string pubsubName, string topicName, TData data, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
            ArgumentVerifier.ThrowIfNull(data, nameof(data));
            return MakePublishRequest(pubsubName, topicName, data, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task PublishEventAsync(string pubsubName, string topicName, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
            ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
            return MakePublishRequest(pubsubName, topicName, string.Empty, cancellationToken);
        }

        private async Task MakePublishRequest<TContent>(string pubsubName, string topicName, TContent content, CancellationToken cancellationToken)
        {
            // Create PublishEventEnvelope
            var envelope = new Autogenerated.PublishEventRequest()
            {
                PubsubName = pubsubName,
                Topic = topicName,
            };

            if (content != null)
            {
                envelope.Data = TypeConverters.ToJsonByteString(content, this.jsonSerializerOptions);
            }

            await this.MakeGrpcCallHandleError(
                options => client.PublishEventAsync(envelope, options),
                cancellationToken);
        }
        #endregion

        #region InvokeBinding Apis

        /// <inheritdoc/>
        public override async Task InvokeBindingAsync<TRequest>(
            string name,
            string operation,
            TRequest data,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentVerifier.ThrowIfNullOrEmpty(operation, nameof(operation));

            _ = await MakeInvokeBindingRequestAsync(name, operation, data, metadata, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<TResponse> InvokeBindingAsync<TRequest, TResponse>(
            string name,
            string operation,
            TRequest data,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentVerifier.ThrowIfNullOrEmpty(operation, nameof(operation));

            Autogenerated.InvokeBindingResponse response = await MakeInvokeBindingRequestAsync(name, operation, data, metadata, cancellationToken);
            return ConvertFromInvokeBindingResponse<TResponse>(response, this.jsonSerializerOptions);
        }

        private static T ConvertFromInvokeBindingResponse<T>(Autogenerated.InvokeBindingResponse response, JsonSerializerOptions options = null)
        {
            var responseData = response.Data.ToStringUtf8();
            return JsonSerializer.Deserialize<T>(responseData, options);
        }

        private async Task<Autogenerated.InvokeBindingResponse> MakeInvokeBindingRequestAsync<TContent>(
           string name,
           string operation,
           TContent data,
           Dictionary<string, string> metadata = default,
           CancellationToken cancellationToken = default)
        {
            var envelope = new Autogenerated.InvokeBindingRequest()
            {
                Name = name,
                Operation = operation
            };

            if (data != null)
            {
                envelope.Data = TypeConverters.ToJsonByteString(data, this.jsonSerializerOptions);
            }

            if (metadata != null)
            {
                envelope.Metadata.Add(metadata);
            }

            return await this.MakeGrpcCallHandleError(
                options => client.InvokeBindingAsync(envelope, options),
                cancellationToken);
        }
        #endregion

        #region InvokeMethod Apis
        public override async Task InvokeMethodAsync(
           string appId,
           string methodName,
           HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            Autogenerated.InvokeServiceRequest request;
            CallOptions callOptions;
            (request, callOptions) = this.MakeInvokeRequestAsync(appId, methodName, null, httpExtension, cancellationToken);
            await client.InvokeServiceAsync(request, callOptions);
        }

        public override async Task InvokeMethodAsync<TRequest>(
           string appId,
           string methodName,
           TRequest data,
           HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            Any serializedData = null;
            if (data != null)
            {
                serializedData = TypeConverters.ToAny(data, this.jsonSerializerOptions);
            }

            Autogenerated.InvokeServiceRequest request;
            CallOptions callOptions;
            (request, callOptions) = this.MakeInvokeRequestAsync(appId, methodName, serializedData, httpExtension, cancellationToken);
            await client.InvokeServiceAsync(request, callOptions);
        }

        public override async ValueTask<TResponse> InvokeMethodAsync<TResponse>(
           string appId,
           string methodName,
           HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            Autogenerated.InvokeServiceRequest request;
            CallOptions callOptions;
            (request, callOptions) = this.MakeInvokeRequestAsync(appId, methodName, null, httpExtension, cancellationToken);
            var response = await client.InvokeServiceAsync(request, callOptions);
            return response.Data.Value.IsEmpty ? default : TypeConverters.FromAny<TResponse>(response.Data, this.jsonSerializerOptions);
        }

        public override async ValueTask<TResponse> InvokeMethodAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            HTTPExtension httpExtension = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            var request = new InvocationRequest<TRequest>
            {
                AppId = appId,
                MethodName = methodName,
                Body = data,
                HttpExtension = httpExtension,
            };

            var invokeResponse = await this.MakeInvokeRequestAsyncWithResponse<TRequest, TResponse>(request, cancellationToken);
            return invokeResponse.Body;
        }

        public override async Task<InvocationResponse<TRequest, TResponse>> InvokeMethodWithResponseAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            Dapr.Client.Http.HTTPExtension httpExtension = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNull(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNull(methodName, nameof(methodName));

            var request = new InvocationRequest<TRequest>
            {
                AppId = appId,
                MethodName = methodName,
                Body = data,
                HttpExtension = httpExtension,
            };

            var invokeResponse = await this.MakeInvokeRequestAsyncWithResponse<TRequest, TResponse>(request, cancellationToken);

            return invokeResponse;
        }

        public override async Task<InvocationResponse<byte[], byte[]>> InvokeMethodRawAsync(
           string appId,
           string methodName,
           byte[] data,
           HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            var request = new InvocationRequest<byte[]>
            {
                AppId = appId,
                MethodName = methodName,
                Body = data,
                HttpExtension = httpExtension,
            };

            var invokeResponse = await this.MakeInvokeRequestAsyncWithResponse<byte[], byte[]>(request, cancellationToken);
            return invokeResponse;
        }

        public override async ValueTask<IReadOnlyList<BulkStateItem>> GetBulkStateAsync(string storeName, IReadOnlyList<string> keys, int? parallelism, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            if (keys.Count == 0)
                throw new ArgumentException("keys do not contain any elements");

            var getBulkStateEnvelope = new Autogenerated.GetBulkStateRequest()
            {
                StoreName = storeName,
                Parallelism = parallelism ?? default(int)
            };

            getBulkStateEnvelope.Keys.AddRange(keys);

            var response = await this.MakeGrpcCallHandleError(
                options => client.GetBulkStateAsync(getBulkStateEnvelope, options),
                cancellationToken);

            var bulkResponse = new List<BulkStateItem>();

            foreach (var item in response.Items)
            {
                bulkResponse.Add(new BulkStateItem(item.Key, item.Data.ToStringUtf8(), item.Etag));
            }

            return bulkResponse;
        }

        private (Autogenerated.InvokeServiceRequest, CallOptions) MakeInvokeRequestAsync(
            string appId,
            string methodName,
            Any data,
            HTTPExtension httpExtension,
            CancellationToken cancellationToken = default)
        {
            var protoHTTPExtension = new Autogenerated.HTTPExtension();
            var contentType = "";
            Metadata headers = null;

            if (httpExtension != null)
            {
                protoHTTPExtension.Verb = ConvertHTTPVerb(httpExtension.Verb);

                if (httpExtension.QueryString != null)
                {
                    foreach (var (key, value) in httpExtension.QueryString)
                    {
                        protoHTTPExtension.Querystring.Add(key, value);
                    }
                }

                if (httpExtension.Headers != null)
                {
                    headers = new Metadata();
                    foreach (var (key, value) in httpExtension.Headers)
                    {
                        headers.Add(key, value);
                    }
                }

                contentType = httpExtension.ContentType ?? Constants.ContentTypeApplicationJson;
            }
            else
            {
                protoHTTPExtension.Verb = Autogenerated.HTTPExtension.Types.Verb.Post;
                contentType = Constants.ContentTypeApplicationJson;
            }

            var invokeRequest = new Autogenerated.InvokeRequest()
            {
                Method = methodName,
                Data = data,
                ContentType = contentType,
                HttpExtension = protoHTTPExtension
            };

            var request = new Autogenerated.InvokeServiceRequest()
            {
                Id = appId,
                Message = invokeRequest,
            };

            var callOptions = new CallOptions(headers: headers ?? new Metadata(), cancellationToken: cancellationToken);

            // add token for dapr api token based authentication
            var daprApiToken = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");

            if (daprApiToken != null)
            {
                callOptions.Headers.Add("dapr-api-token", daprApiToken);
            }

            return (request, callOptions);
        }

        private async Task<InvocationResponse<TRequest, TResponse>> MakeInvokeRequestAsyncWithResponse<TRequest, TResponse>(
            InvocationRequest<TRequest> request,
            CancellationToken cancellationToken = default)
        {
            
            var invokeResponse = new InvocationResponse<TRequest, TResponse>
            {
                Request = request
            };
            
            Any serializedData = null;
            if (request.Body != null)
            {
                if (typeof(TResponse) == typeof(byte[]))
                {
                    // User has passed in raw bytes
                    var requestBytes = (byte[])Convert.ChangeType(request.Body, typeof(byte[]));
                    serializedData = new Any { Value = ByteString.CopyFrom(requestBytes), TypeUrl = typeof(byte[]).FullName };
                }
                else
                {
                    serializedData = TypeConverters.ToAny(request.Body, this.jsonSerializerOptions);
                }
            }

            try
            {
                Autogenerated.InvokeServiceRequest invokeRequest;
                CallOptions callOptions;
                (invokeRequest, callOptions) = this.MakeInvokeRequestAsync(request.AppId, request.MethodName, serializedData, request.HttpExtension, cancellationToken);
                var grpcCall = client.InvokeServiceAsync(invokeRequest, callOptions);

                var response = await grpcCall.ResponseAsync;
                var responseHeaders = await grpcCall.ResponseHeadersAsync;
                var trailers = grpcCall.GetTrailers();
                var grpcStatus = grpcCall.GetStatus();

                var headers = grpcCall.ResponseHeadersAsync.Result.ToDictionary(kv => kv.Key, kv => kv.ValueBytes);

                if (typeof(TResponse) == typeof(byte[]))
                {
                    // User wants to receive raw bytes
                    var responseBytes = new byte[response.Data.Value.Length];
                    response.Data.Value?.CopyTo(responseBytes, 0);
                    invokeResponse.Body = (TResponse)(response.Data.Value.IsEmpty ? default : Convert.ChangeType(responseBytes, typeof(TResponse)));
                }
                else
                {
                    invokeResponse.Body = response.Data.Value.IsEmpty ? default : TypeConverters.FromAny<TResponse>(response.Data, this.jsonSerializerOptions);
                }
                invokeResponse.Headers = headers;
                invokeResponse.Trailers = grpcCall.GetTrailers().ToDictionary(kv => kv.Key, kv => kv.ValueBytes);

                if (headers.TryGetValue(DaprHttpStatusHeader, out var httpStatus))
                {
                    invokeResponse.HttpStatusCode = (HttpStatusCode)System.Enum.Parse(typeof(HttpStatusCode), Encoding.UTF8.GetString(httpStatus, 0, httpStatus.Length));
                    invokeResponse.ContentType = Constants.ContentTypeApplicationJson;
                }
                else
                {
                    // Response is grpc
                    invokeResponse.GrpcStatusInfo = new GrpcStatusInfo(grpcStatus.StatusCode, grpcStatus.Detail);
                    invokeResponse.ContentType = Constants.ContentTypeApplicationGrpc;
                }
            }
            catch (RpcException ex)
            {
                GrpcStatusInfo grpcStatusInfo = null;
                var entry = ex.Trailers.Get(GrpcStatusDetails);
                if (entry != null)
                {
                    var status = Google.Rpc.Status.Parser.ParseFrom(entry.ValueBytes);
                    foreach (var detail in status.Details)
                    {
                        if (Google.Protobuf.WellKnownTypes.Any.GetTypeName(detail.TypeUrl) == GrpcErrorInfoDetail)
                        {
                            var rpcError = detail.Unpack<Google.Rpc.ErrorInfo>();
                            var grpcStatusCode = (StatusCode)status.Code;

                            string innerHttpErrorCode = "0";
                            string innerHttpErrorMessage = string.Empty;
                            rpcError.Metadata.TryGetValue(DaprErrorInfoHttpCodeMetadata, out innerHttpErrorCode);
                            rpcError.Metadata.TryGetValue(DaprErrorInfoHttpErrorMetadata, out innerHttpErrorMessage);
                            grpcStatusInfo = new GrpcStatusInfo(grpcStatusCode, status.Message, Convert.ToInt32(innerHttpErrorCode), innerHttpErrorMessage);
                            break;
                        }
                    }
                }
                throw new InvocationException($"Exception while invoking {request.MethodName} on appId:{request.AppId}", ex, grpcStatusInfo);
            }

            return invokeResponse;
        }

        private bool IsResponseFromHttpCallee(Dictionary<string,byte[]> headers)
        {
            return headers.ContainsKey(DaprHttpStatusHeader);
        }

        #endregion

        #region State Apis
        /// <inheritdoc/>
        public override async ValueTask<TValue> GetStateAsync<TValue>(
            string storeName,
            string key,
            ConsistencyMode? consistencyMode = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var getStateEnvelope = new Autogenerated.GetStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (metadata != null)
            {
                getStateEnvelope.Metadata.Add(metadata);
            }

            if (consistencyMode != null)
            {
                getStateEnvelope.Consistency = GetStateConsistencyForConsistencyMode(consistencyMode.Value);
            }

            var response = await this.MakeGrpcCallHandleError(
                options => client.GetStateAsync(getStateEnvelope, options),
                cancellationToken);

            if (response.Data.IsEmpty)
            {
                return default;
            }

            var responseData = response.Data.ToStringUtf8();
            return JsonSerializer.Deserialize<TValue>(responseData, this.jsonSerializerOptions);
        }

        /// <inheritdoc/>
        public override async ValueTask<(TValue value, string etag)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var getStateEnvelope = new Autogenerated.GetStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (consistencyMode != null)
            {
                getStateEnvelope.Consistency = GetStateConsistencyForConsistencyMode(consistencyMode.Value);
            }

            var response = await this.MakeGrpcCallHandleError(
                options => client.GetStateAsync(getStateEnvelope, options),
                cancellationToken);

            if (response.Data.IsEmpty)
            {
                return (default, response.Etag);
            }

            var responseData = response.Data.ToStringUtf8();
            var deserialized = JsonSerializer.Deserialize<TValue>(responseData, this.jsonSerializerOptions);
            return (deserialized, response.Etag);
        }

        /// <inheritdoc/>
        public override async Task SaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            await this.MakeSaveStateCallAsync(
                storeName,
                key,
                value,
                etag: null,
                stateOptions,
                metadata,
                cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<bool> TrySaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            try
            {
                await this.MakeSaveStateCallAsync(storeName, key, value, etag, stateOptions, metadata, cancellationToken);
                return true;
            }
            catch (RpcException)
            {
            }

            return false;
        }

        private async ValueTask MakeSaveStateCallAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag = default,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            // Create PublishEventEnvelope
            var saveStateEnvelope = new Autogenerated.SaveStateRequest()
            {
                StoreName = storeName,
            };

            var stateItem = new Autogenerated.StateItem()
            {
                Key = key,
            };

            if (metadata != null)
            {
                stateItem.Metadata.Add(metadata);
            }

            if (etag != null)
            {
                stateItem.Etag = etag;
            }

            if (stateOptions != null)
            {
                stateItem.Options = ToAutoGeneratedStateOptions(stateOptions);
            }

            if (value != null)
            {
                stateItem.Value = TypeConverters.ToJsonByteString(value, this.jsonSerializerOptions);
            }

            saveStateEnvelope.States.Add(stateItem);

            await this.MakeGrpcCallHandleError(
                options => client.SaveStateAsync(saveStateEnvelope, options),
                cancellationToken);
        }


        /// <inheritdoc/>
        public override async Task ExecuteStateTransactionAsync(
            string storeName,
            IReadOnlyList<StateTransactionRequest> operations,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNull(operations, nameof(operations));
            if (operations.Count == 0)
            {
                throw new ArgumentException($"{nameof(operations)} does not contain any elements");
            }

            await this.MakeExecuteStateTransactionCallAsync(
                storeName,
                operations,
                metadata,
                cancellationToken);
        }

        private async ValueTask MakeExecuteStateTransactionCallAsync(
            string storeName,
            IReadOnlyList<StateTransactionRequest> states,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            var executeStateTransactionRequestEnvelope = new Autogenerated.ExecuteStateTransactionRequest()
            {
                StoreName = storeName,
            };

            foreach (var state in states)
            {
                var stateOperation = new Autogenerated.TransactionalStateOperation();

                stateOperation.OperationType = state.OperationType.ToString().ToLower();
                stateOperation.Request = ToAutogeneratedStateItem(state);

                executeStateTransactionRequestEnvelope.Operations.Add(stateOperation);

            }

            // Add metadata that applies to all operations if specified
            if (metadata != null)
            {
                executeStateTransactionRequestEnvelope.Metadata.Add(metadata);
            }

            await this.MakeGrpcCallHandleError(
                options => client.ExecuteStateTransactionAsync(executeStateTransactionRequestEnvelope, options),
                cancellationToken);
        }

        private Autogenerated.StateItem ToAutogeneratedStateItem(StateTransactionRequest state)
        {
            var stateOperation = new Autogenerated.StateItem();
            stateOperation.Key = state.Key;

            if (state.Value != null)
            {
                stateOperation.Value = ByteString.CopyFrom(state.Value);
            }

            if (state.ETag != null)
            {
                stateOperation.Etag = state.ETag;
            }

            if (state.Metadata != null)
            {
                stateOperation.Metadata.Add(state.Metadata);
            }

            if (state.Options != null)
            {
                stateOperation.Options = ToAutoGeneratedStateOptions(state.Options);
            }

            return stateOperation;
        }


        /// <inheritdoc/>
        public override async Task DeleteStateAsync(
            string storeName,
            string key,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            await this.MakeDeleteStateCallAsync(
                storeName,
                key,
                etag: null,
                stateOptions,
                metadata,
                cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<bool> TryDeleteStateAsync(
            string storeName,
            string key,
            string etag,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            try
            {
                await this.MakeDeleteStateCallAsync(storeName, key, etag, stateOptions, metadata, cancellationToken);
                return true;
            }
            catch (Exception)
            {
            }

            return false;
        }

        private async ValueTask MakeDeleteStateCallAsync(
           string storeName,
           string key,
           string etag = default,
           StateOptions stateOptions = default,
           Dictionary<string, string> metadata = default,
           CancellationToken cancellationToken = default)
        {
            var deleteStateEnvelope = new Autogenerated.DeleteStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (metadata != null)
            {
                deleteStateEnvelope.Metadata.Add(metadata);
            }

            if (etag != null)
            {
                deleteStateEnvelope.Etag = etag;
            }

            if (stateOptions != null)
            {
                deleteStateEnvelope.Options = ToAutoGeneratedStateOptions(stateOptions);
            }

            await this.MakeGrpcCallHandleError(
                options => client.DeleteStateAsync(deleteStateEnvelope, options),
                cancellationToken);
        }
        #endregion

        #region Secret Apis
        /// <inheritdoc/>
        public async override ValueTask<Dictionary<string, string>> GetSecretAsync(
            string storeName,
            string key,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var envelope = new Autogenerated.GetSecretRequest()
            {
                StoreName = storeName,
                Key = key
            };

            if (metadata != null)
            {
                envelope.Metadata.Add(metadata);
            }

            var response = await this.MakeGrpcCallHandleError(
                 options => client.GetSecretAsync(envelope, options),
                 cancellationToken);

            return response.Data.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Makes Grpc call using the cancellationToken and handles Errors.
        /// All common exception handling logic will reside here.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="callFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private Task<TResponse> MakeGrpcCallHandleError<TResponse>(Func<CallOptions, AsyncUnaryCall<TResponse>> callFunc, CancellationToken cancellationToken = default)
        {
            return MakeGrpcCallHandleError<TResponse>(callFunc, null, cancellationToken);
        }

        /// <summary>
        /// Makes Grpc call using the cancellationToken and handles Errors.
        /// All common exception handling logic will reside here.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="callFunc"></param>
        /// <param name="headers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<TResponse> MakeGrpcCallHandleError<TResponse>(Func<CallOptions, AsyncUnaryCall<TResponse>> callFunc, Metadata headers, CancellationToken cancellationToken = default)
        {
            var callOptions = new CallOptions(headers: headers ?? new Metadata(), cancellationToken: cancellationToken);

            // add token for dapr api token based authentication
            var daprApiToken = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");

            if (daprApiToken != null)
            {
                callOptions.Headers.Add("dapr-api-token", daprApiToken);
            }

            // Common Exception Handling logic can be added here for all calls.
            return await callFunc.Invoke(callOptions);
        }

        private Autogenerated.StateOptions ToAutoGeneratedStateOptions(StateOptions stateOptions)
        {
            var stateRequestOptions = new Autogenerated.StateOptions();

            if (stateOptions.Consistency != null)
            {
                stateRequestOptions.Consistency = GetStateConsistencyForConsistencyMode(stateOptions.Consistency.Value);
            }

            if (stateOptions.Concurrency != null)
            {
                stateRequestOptions.Concurrency = GetStateConcurrencyForConcurrencyMode(stateOptions.Concurrency.Value);
            }

            return stateRequestOptions;
        }

        private static Autogenerated.HTTPExtension.Types.Verb ConvertHTTPVerb(HTTPVerb verb)
        {
            return verb switch
            {
                HTTPVerb.Get => Autogenerated.HTTPExtension.Types.Verb.Get,
                HTTPVerb.Head => Autogenerated.HTTPExtension.Types.Verb.Head,
                HTTPVerb.Post => Autogenerated.HTTPExtension.Types.Verb.Post,
                HTTPVerb.Put => Autogenerated.HTTPExtension.Types.Verb.Put,
                HTTPVerb.Delete => Autogenerated.HTTPExtension.Types.Verb.Delete,
                HTTPVerb.Connect => Autogenerated.HTTPExtension.Types.Verb.Connect,
                HTTPVerb.Options => Autogenerated.HTTPExtension.Types.Verb.Options,
                HTTPVerb.Trace => Autogenerated.HTTPExtension.Types.Verb.Trace,
                _ => throw new NotImplementedException($"Service invocation with verb '{verb}' is not supported")
            };
        }

        private static Autogenerated.StateOptions.Types.StateConsistency GetStateConsistencyForConsistencyMode(ConsistencyMode consistencyMode)
        {
            return consistencyMode switch
            {
                ConsistencyMode.Eventual => Autogenerated.StateOptions.Types.StateConsistency.ConsistencyEventual,
                ConsistencyMode.Strong => Autogenerated.StateOptions.Types.StateConsistency.ConsistencyStrong,
                _ => throw new ArgumentException($"{consistencyMode} Consistency Mode is not supported.")
            };
        }

        private static Autogenerated.StateOptions.Types.StateConcurrency GetStateConcurrencyForConcurrencyMode(ConcurrencyMode concurrencyMode)
        {
            return concurrencyMode switch
            {
                ConcurrencyMode.FirstWrite => Autogenerated.StateOptions.Types.StateConcurrency.ConcurrencyFirstWrite,
                ConcurrencyMode.LastWrite => Autogenerated.StateOptions.Types.StateConcurrency.ConcurrencyLastWrite,
                _ => throw new ArgumentException($"{concurrencyMode} Concurrency Mode is not supported.")
            };
        }
        #endregion Helper Methods
    }
}
