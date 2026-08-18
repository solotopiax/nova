/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAcquisitionTrackPlugin.cs
 * author:    taoye
 * created:   2026/8/17
 * descrip:   Acquisition tracking SDK plugin interface
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Acquisition tracking interface for ad delivery and user acquisition conversion events.
    /// </summary>
    public interface IAcquisitionTrackPlugin : ISDKPlugin
    {
        /// <summary>
        /// Sets the current business user ID for acquisition event attribution.
        /// </summary>
        /// <param name="userId">Business user identifier.</param>
        void SetUserId(string userId);

        /// <summary>
        /// Tracks an acquisition event by the common event payload.
        /// </summary>
        /// <param name="evt">Event payload.</param>
        void TrackEvent(TrackEvent evt);

        /// <summary>
        /// Tracks an acquisition event by event name and parameters.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="parameters">Event parameters.</param>
        void TrackEvent(string eventName, Dictionary<string, object> parameters);
    }
}
