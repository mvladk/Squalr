﻿namespace Ana.Source.Project.ProjectItems
{
    using Controls;
    using Editors.HotkeyEditor;
    using Editors.TextEditor;
    using Engine.Input.HotKeys;
    using SharpDX.DirectInput;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing.Design;
    using System.Runtime.Serialization;
    using Utils.TypeConverters;

    /// <summary>
    /// A base class for all project items that can be added to the project explorer.
    /// </summary>
    [KnownType(typeof(ProjectItem))]
    [KnownType(typeof(FolderItem))]
    [KnownType(typeof(ScriptItem))]
    [KnownType(typeof(AddressItem))]
    [DataContract]
    internal abstract class ProjectItem : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// The parent of this project item.
        /// </summary>
        [Browsable(false)]
        private FolderItem parent;

        /// <summary>
        /// The description of this project item.
        /// </summary>
        [Browsable(false)]
        private String description;

        /// <summary>
        /// The extended description of this project item.
        /// </summary>
        [Browsable(false)]
        private String extendedDescription;

        /// <summary>
        /// The unique identifier of this project item.
        /// </summary>
        [Browsable(false)]
        private Guid guid;

        /// <summary>
        /// The hotkey associated with this project item.
        /// </summary>
        [Browsable(false)]
        private Hotkey hotkey;

        /// <summary>
        /// A value indicating whether this project item has been activated.
        /// </summary>
        [Browsable(false)]
        private Boolean isActivated;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectItem" /> class.
        /// </summary>
        public ProjectItem() : this(String.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectItem" /> class.
        /// </summary>
        /// <param name="description">The description of the project item.</param>
        public ProjectItem(String description)
        {
            // Bypass setters/getters to avoid triggering any view updates in constructor
            this.description = description == null ? String.Empty : description;
            this.parent = null;
            this.isActivated = false;
            this.guid = Guid.NewGuid();
        }

        /// <summary>
        /// Occurs after a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the parent of this project item.
        /// </summary>
        [Browsable(false)]
        public FolderItem Parent
        {
            get
            {
                return this.parent;
            }

            set
            {
                this.parent = value;
                this.NotifyPropertyChanged(nameof(this.Parent));
            }
        }

        /// <summary>
        /// Gets or sets the description for this object.
        /// </summary>
        [DataMember]
        [SortedCategory(SortedCategory.CategoryType.Common), DisplayName("Description"), Description("Description to be shown for the Project Items")]
        public String Description
        {
            get
            {
                return this.description;
            }

            set
            {
                if (this.description == value)
                {
                    return;
                }

                this.description = value;
                ProjectExplorerViewModel.GetInstance().HasUnsavedChanges = true;
                this.NotifyPropertyChanged(nameof(this.Description));
                ProjectExplorerViewModel.GetInstance().OnPropertyUpdate();
            }
        }

        /// <summary>
        /// Gets or sets the description for this object.
        /// </summary>
        [DataMember]
        [TypeConverter(typeof(TextConverter))]
        [Editor(typeof(TextEditorModel), typeof(UITypeEditor))]
        [SortedCategory(SortedCategory.CategoryType.Common), DisplayName("Extended Description"), Description("Extended description for additional information about this item")]
        public String ExtendedDescription
        {
            get
            {
                return this.extendedDescription;
            }

            set
            {
                if (this.extendedDescription == value)
                {
                    return;
                }

                this.extendedDescription = value;
                ProjectExplorerViewModel.GetInstance().HasUnsavedChanges = true;
                this.NotifyPropertyChanged(nameof(this.ExtendedDescription));
                ProjectExplorerViewModel.GetInstance().OnPropertyUpdate();
            }
        }

        /// <summary>
        /// Gets or sets the unique identifier of this project item.
        /// </summary>
        [DataMember]
        [Browsable(false)]
        public Guid Guid
        {
            get
            {
                return this.guid;
            }

            set
            {
                if (this.guid == value)
                {
                    return;
                }

                this.guid = value;
                ProjectExplorerViewModel.GetInstance().HasUnsavedChanges = true;
                this.NotifyPropertyChanged(nameof(this.Guid));
                ProjectExplorerViewModel.GetInstance().OnPropertyUpdate();
            }
        }

        /// <summary>
        /// Gets or sets the hotkey for this project item.
        /// </summary>
        [TypeConverter(typeof(HotkeyConverter))]
        [Editor(typeof(HotkeyEditorModel), typeof(UITypeEditor))]
        [SortedCategory(SortedCategory.CategoryType.Common), DisplayName("Hotkey"), Description("The hotkey for this item")]
        public Hotkey HotKey
        {
            get
            {
                return this.hotkey;
            }

            set
            {
                if (this.hotkey == value)
                {
                    return;
                }

                this.hotkey = value;
                this.HotKey?.SetCallBackFunction(() => this.IsActivated = !this.IsActivated);
                ProjectExplorerViewModel.GetInstance().HasUnsavedChanges = true;
                this.NotifyPropertyChanged(nameof(this.HotKey));
                ProjectExplorerViewModel.GetInstance().OnPropertyUpdate();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not this item is activated.
        /// </summary>
        [Browsable(false)]
        public Boolean IsActivated
        {
            get
            {
                return this.CanActivate && this.isActivated;
            }

            set
            {
                if (this.isActivated == value || !this.CanActivate)
                {
                    return;
                }

                Boolean previousValue = this.isActivated;

                this.isActivated = value;
                this.OnActivationChanged();

                // Activation failed
                if (this.isActivated == previousValue)
                {
                    return;
                }

                this.NotifyPropertyChanged(nameof(this.IsActivated));
                ProjectExplorerViewModel.GetInstance().OnPropertyUpdate();
            }
        }

        /// <summary>
        /// Gets a value indicating whether or not this item can be activated.
        /// </summary>
        [Browsable(false)]
        public Boolean CanActivate
        {
            get
            {
                return this.IsActivatable();
            }
        }

        /// <summary>
        /// Invoked when this object is deserialized.
        /// </summary>
        /// <param name="streamingContext">Streaming context.</param>
        [OnDeserialized]
        public void OnDeserialized(StreamingContext streamingContext)
        {
            if (this.Guid == null || this.Guid == Guid.Empty)
            {
                this.guid = Guid.NewGuid();
            }
        }

        /// <summary>
        /// Updates event for this project item.
        /// </summary>
        public abstract void Update();

        /// <summary>
        /// Reconstructs the parents for all nodes of this graph. Call this from the root.
        /// Needed since we cannot serialize the parent to json or we will get cyclic dependencies.
        /// </summary>
        /// <param name="parent">The parent of this project item.</param>
        public virtual void BuildParents(FolderItem parent = null)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// Clones the project item.
        /// </summary>
        /// <returns>The clone of the project item.</returns>
        public abstract ProjectItem Clone();

        /// <summary>
        /// Updates the hotkey, bypassing setters to avoid triggering view updates.
        /// </summary>
        /// <param name="hotkey">The hotkey for this project item.</param>
        public void LoadHotkey(Hotkey hotkey)
        {
            this.hotkey = hotkey;

            this.HotKey?.SetCallBackFunction(() => this.IsActivated = !this.IsActivated);
        }

        public void Dispose()
        {
            this.HotKey?.Dispose();
        }

        /// <summary>
        /// Randomizes the guid of this project item.
        /// </summary>
        public void ResetGuid()
        {
            this.Guid = Guid.NewGuid();
        }

        /// <summary>
        /// Event received when a key is released.
        /// </summary>
        /// <param name="key">The key that was released.</param>
        public void OnKeyPress(Key key)
        {
        }

        /// <summary>
        /// Event received when a key is down.
        /// </summary>
        /// <param name="key">The key that is down.</param>
        public void OnKeyRelease(Key key)
        {
        }

        /// <summary>
        /// Event received when a key is down.
        /// </summary>
        /// <param name="key">The key that is down.</param>
        public void OnKeyDown(Key key)
        {
        }

        /// <summary>
        /// Event received when a set of keys are down.
        /// </summary>
        /// <param name="pressedKeys">The down keys.</param>
        public void OnUpdateAllDownKeys(HashSet<Key> pressedKeys)
        {
            if (this.HotKey is KeyboardHotkey)
            {
                KeyboardHotkey keyboardHotkey = this.HotKey as KeyboardHotkey;
            }
        }

        /// <summary>
        /// Indicates that a given property in this project item has changed.
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        protected void NotifyPropertyChanged(String propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Deactivates this item without triggering the <see cref="OnActivationChanged" /> function.
        /// </summary>
        protected void ResetActivation()
        {
            this.isActivated = false;
            this.NotifyPropertyChanged(nameof(this.IsActivated));
        }

        /// <summary>
        /// Called when the activation state changes.
        /// </summary>
        protected virtual void OnActivationChanged()
        {
        }

        /// <summary>
        /// Overridable function indicating if this script can be activated.
        /// </summary>
        /// <returns>True if the script can be activated, otherwise false.</returns>
        protected virtual Boolean IsActivatable()
        {
            return true;
        }
    }
    //// End class
}
//// End namespace