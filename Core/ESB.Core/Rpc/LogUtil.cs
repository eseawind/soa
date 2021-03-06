﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ESB.Core.Entity;

namespace ESB.Core.Rpc
{
    public class LogUtil
    {
        /// <summary>
        /// 增加审计日志
        /// </summary>
        public static String AddAuditLog(int status
            , String bindingTemplateID
            , String serviceID
            , String address
            , DateTime reqBeginTime
            , DateTime reqEndTime
            , DateTime callBeginTime
            , DateTime callEndTime
            , String message
            , ESB.Core.Schema.服务请求 request)
        {
            AuditBusiness log = new AuditBusiness()
            {
                OID = Guid.NewGuid().ToString(),
                HostName = request.主机名称,
                ServiceName = request.服务名称,
                MethodName = request.方法名称,
                ReqBeginTime = reqBeginTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                ReqEndTime = reqEndTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Status = status,
                MessageID = Guid.NewGuid().ToString(),
                MessageBody = request.消息内容,
                CallBeginTime = callBeginTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                CallEndTime = callEndTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                BindingTemplateID = bindingTemplateID,
                ServiceID = serviceID,
                BindingAddress = address,
                ReturnMessageBody = message,
                BusinessName = "",
                BusinessID = ""
            };

            //log.Insert();

            MQUtil.SendMessage<AuditBusiness>(log, @"WIN-02UQME9JGZD\Private$\EsbAuditQueue");

            return log.MessageID;
        }


        /// <summary>
        /// 增加审计日志
        /// </summary>
        public static String AddAuditLog(int status
            , BindingTemplate binding
            , DateTime reqBeginTime
            , DateTime reqEndTime
            , DateTime callBeginTime
            , DateTime callEndTime
            , String message
            , ESB.Core.Schema.服务请求 request)
        {
            return AddAuditLog(status, binding.TemplateID.ToString(), binding.ServiceID.ToString(), binding.Address.ToString()
             , reqBeginTime, reqEndTime, callBeginTime, callEndTime
             , message, request);
        }

        /// <summary>
        /// 抛出异常并写日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="binding"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public static Exception ExceptionAndLog(String exceptionDesc, String message, ESB.Core.Schema.服务请求 request)
        {
            return ExceptionAndLog(exceptionDesc, message, null, request);
        }

        /// <summary>
        /// 抛出异常并写日志
        /// </summary>
        /// <param name="message"></param>
        /// <param name="binding"></param>
        /// <param name="request"></param>
        public static Exception ExceptionAndLog(String exceptionDesc, String message, BindingTemplate binding, ESB.Core.Schema.服务请求 request)
        {
            CallState state = new CallState()
            {
                RequestBeginTime = DateTime.Now,
                RequestEndTime = DateTime.Now,
                CallBeginTime = DateTime.Now,
                CallEndTime = DateTime.Now
            };

            return ExceptionAndLog(state, exceptionDesc, message, binding, request);
        }

        /// <summary>
        /// 抛出异常并写日志, 并需要记录异常的时间
        /// </summary>
        /// <param name="message"></param>
        /// <param name="binding"></param>
        /// <param name="request"></param>
        public static Exception ExceptionAndLog(CallState state, String exceptionDesc, String message, BindingTemplate binding, ESB.Core.Schema.服务请求 request)
        {
            String messageID = String.Empty;
            String exceptionMessage = String.Format("{0}：{1}", exceptionDesc, message);


            if (state.CallBeginTime.Year != DateTime.Now.Year) state.CallBeginTime = DateTime.Now;
            if (state.CallEndTime.Year != DateTime.Now.Year) state.CallEndTime = DateTime.Now;

            if (binding != null)
                messageID = AddAuditLog(0, binding, state.RequestBeginTime, state.RequestEndTime, state.CallBeginTime, state.CallEndTime
                    , exceptionMessage, request);
            else
                messageID = AddAuditLog(0, "00000000-0000-0000-0000-000000000000"
                    , String.Empty, string.Empty, state.RequestBeginTime, state.RequestEndTime, state.CallBeginTime, state.CallEndTime
                    , exceptionMessage, request);


            ExceptionCoreTb exception = new ExceptionCoreTb()
            {
                ExceptionID = Guid.NewGuid(),
                BindingTemplateID = binding == null ? Guid.Empty : binding.TemplateID,
                BindingType = 0,
                Description = exceptionDesc,
                ExceptionCode = String.Empty,
                ExceptionInfo = exceptionMessage,
                ExceptionLevel = 0,
                ExceptionStatus = 0,
                ExceptionTime = DateTime.Now,
                ExceptionType = 0,
                HostName = request.主机名称,
                MessageBody = request.消息内容,
                MessageID = new Guid(messageID),
                MethodName = request.方法名称,
                RequestPwd = request.密码,
                RequestType = 0
            };
            exception.Insert();

            return new Exception(message);
        }
    }
}