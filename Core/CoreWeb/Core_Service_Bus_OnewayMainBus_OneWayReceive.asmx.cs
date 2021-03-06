﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using ESB.Core.Rpc;

namespace CoreWeb
{
    /// <summary>
    /// 服务“Core.Service.Bus.OnewayMainBus”端口“OneWayReceive”
    /// </summary>
    [System.Web.Services.WebServiceBindingAttribute(ConformsTo = System.Web.Services.WsiProfiles.None, EmitConformanceClaims = false)]
    [System.Web.Services.WebServiceAttribute(Name = "Core_Service_Bus_OnewayMainBus_OneWayReceive", Namespace = "http://www.jn.com/Esb", Description = "BizTalk 程序集“Core.Service.Bus, Version=1.0.0.0, Culture=neutral, PublicKeyToken=ee" +
        "83b787fd1ed5a4”已发布 Web Services。")]
    [System.Web.Services.Protocols.SoapDocumentServiceAttribute(Use = System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle = System.Web.Services.Protocols.SoapParameterStyle.Default)]
    public sealed class Core_Service_Bus_OnewayMainBus_OneWayReceive : System.Web.Services.WebService
    {

        /// <summary>
        /// 操作“ReceiveRequest”
        /// </summary>
        [System.Web.Services.WebMethodAttribute()]
        [System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://www.jn.com/Esb/Core_Service_Bus_OnewayMainBus_OneWayReceive/ReceiveRequest" +
            "", OneWay = true, Use = System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle = System.Web.Services.Protocols.SoapParameterStyle.Default)]
        public void ReceiveRequest([System.Xml.Serialization.XmlElementAttribute(Namespace = "http://schemas.jn.com/esb/request/20100329", ElementName = "服务请求")] ESB.Core.Schema.服务请求 part)
        {
            EsbClient.DynamicalCallWebService(false, part);
        }
    }

}
