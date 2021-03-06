﻿namespace Ana.Source.Utils.Extensions
{
    using Observables;
    using System;

    /// <summary>
    /// Extension methods for the IDisposable interface.
    /// </summary>
    internal static class IDisposableExtensions
    {
        public static IDisposable WeakSubscribe<T>(this IObservable<T> observable, IObserver<T> observer)
        {
            return new WeakObserver<T>(observable, observer);
        }
    }
    //// End class
}
//// End namespace