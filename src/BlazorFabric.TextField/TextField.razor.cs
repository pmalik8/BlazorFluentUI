﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorFabric
{
    public partial class TextField : FabricComponentBase
    {
        [Inject] private IJSRuntime JSRuntime { get; set; }

        [Parameter] public bool Required { get; set; }
        [Parameter] public bool Multiline { get; set; }
        [Parameter] public InputType InputType { get; set; } = InputType.Text;
        [Parameter] public bool Resizable { get; set; } = true;
        [Parameter] public bool AutoAdjustHeight { get; set; }
        [Parameter] public bool Underlined { get; set; }
        [Parameter] public bool Borderless { get; set; }
        [Parameter] public string Label { get; set; }
        [Parameter] public RenderFragment RenderLabel { get; set; }
        [Parameter] public string Description { get; set; }
        [Parameter] public string Prefix { get; set; }
        [Parameter] public string Suffix { get; set; }
        [Parameter] public string DefaultValue { get; set; }
        [Parameter] public string Value { get; set; }
        [Parameter] public bool Disabled { get; set; }
        [Parameter] public bool ReadOnly { get; set; }
        [Parameter] public string ErrorMessage { get; set; }
        [Parameter] public bool ValidateOnFocusIn { get; set; }
        [Parameter] public bool ValidateOnFocusOut { get; set; }
        [Parameter] public bool ValidateOnLoad { get; set; } = true;
        [Parameter] public int DeferredValidationTime { get; set; } = 200;
        [Parameter] public AutoComplete AutoComplete { get; set; } = AutoComplete.On;
        [Parameter] public string Placeholder { get; set; }
        [Parameter] public string IconName { get; set; }
        
        [Parameter]
        public Func<string, string> OnGetErrorMessage { get; set; }
        [Parameter]
        public Action<string, string> OnNotifyValidationResult { get; set; }

        // Events
        [Parameter]
        public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }
        [Parameter]
        public EventCallback<KeyboardEventArgs> OnKeyUp { get; set; }
        [Parameter]
        public EventCallback<KeyboardEventArgs> OnKeyPress { get; set; }
        [Parameter]
        public EventCallback<MouseEventArgs> OnMouseDown { get; set; }
        [Parameter]
        public EventCallback<MouseEventArgs> OnMouseUp { get; set; }
        [Parameter]
        public EventCallback<ClipboardEventArgs> OnPaste { get; set; }
        [Parameter]
        public EventCallback<MouseEventArgs> OnClick { get; set; }  // expose click event for Combobox and pickers
        [Parameter]
        public EventCallback<FocusEventArgs> OnBlur { get; set; }
        [Parameter]
        public EventCallback<FocusEventArgs> OnFocus { get; set; }
        [Parameter]
        public EventCallback<string> OnChange { get; set; }
        [Parameter]
        public EventCallback<string> OnInput { get; set; }

        
        public ElementReference textAreaRef;

        protected string id = Guid.NewGuid().ToString();
        protected string descriptionId = Guid.NewGuid().ToString();
        protected string inlineTextAreaStyle = "";
        protected bool isFocused = false;

        private bool firstRendered = false;
        private int deferredValidationTime;
        private bool defaultErrorMessageIsSet;
        private string latestValidatedValue = "";
        private string currentValue;
        private ICollection<Task> DeferredValidationTasks = new List<Task>();

        protected string CurrentValue
        {
            get => currentValue;
            set
            {
                if (value == currentValue)
                    return;
                currentValue = value;
                OnChangeIntern(new ChangeEventArgs() { Value = value }).ConfigureAwait(true);
            }
        }


        protected override Task OnInitializedAsync()
        {
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                defaultErrorMessageIsSet = true;
            }

            // to prevent changes after initialisation
            deferredValidationTime = DeferredValidationTime;


            return base.OnInitializedAsync();
        }

        protected override Task OnParametersSetAsync()
        {
            if (DefaultValue != null)
                CurrentValue = DefaultValue;

            if (Value != null)
                CurrentValue = Value;

            if (ValidateOnLoad && ValidateAllChanges())
            {
                Validate(CurrentValue);
            }

            return base.OnParametersSetAsync();
        }

        protected async Task OnInputIntern(ChangeEventArgs args)
        {

            if (!defaultErrorMessageIsSet && OnGetErrorMessage != null && !string.IsNullOrWhiteSpace(ErrorMessage))
            {
                ErrorMessage = "";
                await InvokeAsync(() => StateHasChanged());
            }
            if (ValidateAllChanges())
            {
                await DeferredValidation((string)args.Value).ConfigureAwait(false);
            }

            await AdjustInputHeightAsync();

            if (OnInput.HasDelegate)
                await OnInput.InvokeAsync((string)args.Value);

        }

        protected async Task OnChangeIntern(ChangeEventArgs args)
        {
            if(OnChange.HasDelegate)
                await OnChange.InvokeAsync((string)args.Value);
        }

        protected async Task OnFocusInternal(FocusEventArgs args)
        {
            if (OnFocus.HasDelegate)
                await OnFocus.InvokeAsync(args);

            isFocused = true;
            Console.WriteLine("OnFocus TextField"); 
            if (ValidateOnFocusIn && !defaultErrorMessageIsSet)
            {
                Validate(CurrentValue);
            }
            //return Task.CompletedTask;
        }

        protected async Task OnBlurInternal(FocusEventArgs args)
        {
            if (OnBlur.HasDelegate)
                await OnBlur.InvokeAsync(args);

            Console.WriteLine("OnFocus TextField"); 
            isFocused = false;
            if (ValidateOnFocusOut && !defaultErrorMessageIsSet)
            {
                Validate(CurrentValue);
            }
            //return Task.CompletedTask;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRendered)
            {
                firstRendered = true;
                await AdjustInputHeightAsync();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        protected string GetAutoCompleteString()
        {
            var value = AutoComplete.ToString();
            value = Char.ToLowerInvariant(value[0]) + value.Substring(1);
            string result = "";
            foreach (var c in value.ToCharArray())
            {
                if (Char.IsUpper(c))
                {
                    result += "-";
                    result += Char.ToLowerInvariant(c);
                }
                else
                    result += c;
            }
            return result;
        }

        private async Task AdjustInputHeightAsync()
        {
            if (AutoAdjustHeight == true && Multiline)
            {
                var scrollHeight = await JSRuntime.InvokeAsync<double>("BlazorFabricTextField.getScrollHeight", textAreaRef);
                inlineTextAreaStyle = $"height: {scrollHeight}px";
            }
        }

        private void Validate(string value)
        {
            if (value == null || latestValidatedValue == value)
                return;

            latestValidatedValue = value;
            string errorMessage = OnGetErrorMessage?.Invoke(value);
            if (errorMessage != null)
            {
                ErrorMessage = errorMessage;
            }
            OnNotifyValidationResult?.Invoke(errorMessage, value);
        }

        private bool ValidateAllChanges()
        {
            return OnGetErrorMessage != null && !defaultErrorMessageIsSet && !ValidateOnFocusIn && !ValidateOnFocusOut;
        }

        private async Task DeferredValidation(string value)
        {
            DeferredValidationTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(deferredValidationTime);
            }));
            var TaskCount = DeferredValidationTasks.Count();
            await Task.WhenAll(DeferredValidationTasks.ToArray());
            if (TaskCount == DeferredValidationTasks.Count())
            {
                _ = Task.Run(() =>
                  {
                      Validate(value);
                      InvokeAsync(()=>StateHasChanged());  //invokeasync required for serverside
                  }).ConfigureAwait(false);
            }
        }
    }
}