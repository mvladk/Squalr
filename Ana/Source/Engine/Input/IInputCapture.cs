﻿namespace Ana.Source.Engine.Input
{
    /// <summary>
    /// An interface defining an object responsable for capturing input for a specific device
    /// </summary>
    internal interface IInputCapture
    {
        /// <summary>
        /// Updates the input capture device, polling the system for changes on that device
        /// </summary>
        void Update();
    }
    //// End class
}
//// End namespace