﻿using System;

namespace TournamentAssistantServer.PacketService.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class AllowUnauthorized : Attribute
    {
    }
}