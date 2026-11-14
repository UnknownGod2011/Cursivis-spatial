using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cursivis.Spatial
{
    public enum FlowState
    {
        Idle,
        SelectionReady,     // world-snip mode armed, waiting for pinch-drag
        Selecting,          // pinch-drag in progress
        SelectionConfirmed, // selection captured, "Ask Cursivis" chip visible
        Understanding,      // suggestions requested
        SuggestionsVisible, // four suggestions + Talk
        ActionSelected,     // user chose a suggestion (transient)
        VoiceListening,     // push-to-talk recording
        Processing,         // action / talk request in flight
        ResultVisible,      // result card shown
        FollowUp,           // follow-up in flight from the result card
        Dismissed           // transient, returns to Idle
    }

    /// <summary>
    /// Explicit transition table so the UI can never get into conflicting states.
    /// Any illegal transition is rejected and logged instead of silently applied.
    /// </summary>
    public sealed class FlowStateMachine
    {
        static readonly Dictionary<FlowState, FlowState[]> Allowed = new Dictionary<FlowState, FlowState[]>
        {
            // Idle -> Processing: typed general requests (follow-up chips / voice client actions) without a selection.
            { FlowState.Idle, new[] { FlowState.SelectionReady, FlowState.SelectionConfirmed, FlowState.VoiceListening, FlowState.Processing } },
            { FlowState.SelectionReady, new[] { FlowState.Selecting, FlowState.Idle, FlowState.SelectionConfirmed } },
            { FlowState.Selecting, new[] { FlowState.SelectionConfirmed, FlowState.SelectionReady, FlowState.Idle } },
            // -> Processing from SelectionConfirmed / Understanding / SuggestionsVisible: a typed Talk request
            // (follow-up chip, voice client action) about the current selection skips VoiceListening.
            { FlowState.SelectionConfirmed, new[] { FlowState.Understanding, FlowState.SelectionReady, FlowState.SelectionConfirmed, FlowState.Idle, FlowState.VoiceListening, FlowState.Processing } },
            { FlowState.Understanding, new[] { FlowState.SuggestionsVisible, FlowState.SelectionConfirmed, FlowState.VoiceListening, FlowState.Idle, FlowState.SelectionReady, FlowState.Processing } },
            { FlowState.SuggestionsVisible, new[] { FlowState.ActionSelected, FlowState.VoiceListening, FlowState.SelectionConfirmed, FlowState.SelectionReady, FlowState.Idle, FlowState.Understanding, FlowState.Processing } },
            { FlowState.ActionSelected, new[] { FlowState.Processing, FlowState.SuggestionsVisible, FlowState.Idle } },
            { FlowState.VoiceListening, new[] { FlowState.Processing, FlowState.SuggestionsVisible, FlowState.ResultVisible, FlowState.SelectionConfirmed, FlowState.Idle } },
            { FlowState.Processing, new[] { FlowState.ResultVisible, FlowState.SuggestionsVisible, FlowState.SelectionConfirmed, FlowState.Idle, FlowState.SelectionReady } },
            { FlowState.ResultVisible, new[] { FlowState.FollowUp, FlowState.VoiceListening, FlowState.Understanding, FlowState.SuggestionsVisible, FlowState.Dismissed, FlowState.SelectionReady, FlowState.SelectionConfirmed, FlowState.Processing, FlowState.Idle } },
            { FlowState.FollowUp, new[] { FlowState.ResultVisible, FlowState.Idle } },
            { FlowState.Dismissed, new[] { FlowState.Idle, FlowState.SelectionReady, FlowState.SelectionConfirmed } },
        };

        public FlowState State { get; private set; } = FlowState.Idle;
        public float EnteredAt { get; private set; }
        public event Action<FlowState, FlowState> Changed;

        public bool CanGo(FlowState to) => to == State || (Allowed.TryGetValue(State, out var list) && Array.IndexOf(list, to) >= 0);

        public bool Go(FlowState to, string reason = null)
        {
            if (to == State) return true;
            if (!CanGo(to))
            {
                Debug.LogWarning($"[CursivisFlow] rejected {State} -> {to} ({reason})");
                return false;
            }
            var from = State;
            State = to;
            EnteredAt = Time.realtimeSinceStartup;
            Debug.Log($"[CursivisFlow] {from} -> {to}{(reason != null ? " (" + reason + ")" : "")}");
            Changed?.Invoke(from, to);
            return true;
        }

        /// <summary>Hard reset used for app pause/resume and fatal errors.</summary>
        public void Reset()
        {
            var from = State;
            State = FlowState.Idle;
            EnteredAt = Time.realtimeSinceStartup;
            if (from != FlowState.Idle) Changed?.Invoke(from, FlowState.Idle);
        }
    }
}
