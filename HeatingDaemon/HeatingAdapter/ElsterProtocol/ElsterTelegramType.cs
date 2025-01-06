using System;

namespace HeatingDaemon;
    /// <summary>
    /// The possible types of Elster Telegrams.
    /// </summary>
    /// <remarks>
    ///  Quelle:https://github.com/robots/Elster/blob/master/TelegramType.py
    ///  </remarks>
    public enum ElsterTelegramType
    {
        /// <summary>
        /// A write telegram to the Elster device.
        /// </summary>
        Write = 0,
        /// <summary>
        /// A read telegram to the Elster device.
        /// </summary>
        Read = 1,
        /// <summary>
        /// A respond telegram to the Elster device.
        /// </summary>
        Respond = 2,
        /// <summary>
        /// An acknowledge telegram to the Elster device.
        /// </summary>
        Acknowledge = 3,
        /// <summary>
        /// A write telegram with acknowledge to the Elster device.
        /// </summary>
        WriteAcknowledge = 4,
        /// <summary>
        /// A write telegram with respond to the Elster device.
        /// </summary>
        WriteRespond = 5,
        /// <summary>
        /// A system telegram to the Elster device.
        /// </summary>
        System = 6,
        /// <summary>
        /// A respond system telegram to the Elster device.
        /// </summary>
        RespondSystem = 7,
        /// <summary>
        /// An unknown telegram to/from the Elster device.
        /// </summary>
        Unknown = 255
    }
   
