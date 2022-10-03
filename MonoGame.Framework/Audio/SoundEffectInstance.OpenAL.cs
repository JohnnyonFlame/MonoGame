// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;

#if MONOMAC && PLATFORM_MACOS_LEGACY
using MonoMac.OpenAL;
#endif
#if MONOMAC && !PLATFORM_MACOS_LEGACY
using OpenTK.Audio.OpenAL;
#endif
#if GLES
using OpenTK.Audio.OpenAL;
#endif
#if DESKTOPGL
using OpenAL;
#endif

namespace Microsoft.Xna.Framework.Audio
{
    public partial class SoundEffectInstance : IDisposable
    {
		internal SoundState SoundState = SoundState.Stopped;
		private bool _looped = false;
		private float _alVolume = 1;

		internal int SourceId;
        private float reverb = 0f;
        bool applyFilter = false;
#if SUPPORTS_EFX
        EfxFilterType filterType;
#endif
        float filterQ;
        float frequency;
        int pauseCount;
        int[] buffers;
        
        internal OpenALSoundController controller;
        
        internal bool HasSourceId = false;

        internal long currentBufferPosition;

#region Initialization

        /// <summary>
        /// Creates a standalone SoundEffectInstance from given wavedata.
        /// </summary>
        internal void PlatformInitialize(byte[] buffer, int sampleRate, int channels)
        {
            InitializeSound();
        }

        /// <summary>
        /// Gets the OpenAL sound controller, constructs the sound buffer, and sets up the event delegates for
        /// the reserved and recycled events.
        /// </summary>
        internal void InitializeSound()
        {
            controller = OpenALSoundController.GetInstance;
        }

#endregion // Initialization

        /// <summary>
        /// Converts the XNA [-1, 1] pitch range to OpenAL pitch (0, INF) or Android SoundPool playback rate [0.5, 2].
        /// <param name="xnaPitch">The pitch of the sound in the Microsoft XNA range.</param>
        /// </summary>
        private static float XnaPitchToAlPitch(float xnaPitch)
        {
            return (float)Math.Pow(2, xnaPitch);
        }

        private void PlatformApply3D(AudioListener listener, AudioEmitter emitter)
        {
            // get AL's listener position
            float x, y, z;
            AL.GetListener(ALListener3f.Position, out x, out y, out z);
            ALHelper.CheckError("Failed to get source position.");

            // get the emitter offset from origin
            Vector3 posOffset = emitter.Position - listener.Position;
            // set up orientation matrix
            Matrix orientation = Matrix.CreateWorld(Vector3.Zero, listener.Forward, listener.Up);
            // set up our final position and velocity according to orientation of listener
            Vector3 finalPos = new Vector3(x + posOffset.X, y + posOffset.Y, z + posOffset.Z);
            finalPos = Vector3.Transform(finalPos, orientation);
            Vector3 finalVel = emitter.Velocity;
            finalVel = Vector3.Transform(finalVel, orientation);

            // set the position based on relative positon
            AL.Source(SourceId, ALSource3f.Position, finalPos.X, finalPos.Y, finalPos.Z);
            ALHelper.CheckError("Failed to set source position.");
            AL.Source(SourceId, ALSource3f.Velocity, finalVel.X, finalVel.Y, finalVel.Z);
            ALHelper.CheckError("Failed to Set source velocity.");
        }

        private void PlatformPause()
        {
            if (!HasSourceId || SoundState != SoundState.Playing)
                return;

            if (!controller.CheckInitState())
            {
                return;
            }

            if (pauseCount == 0)
            {
                AL.SourcePause(SourceId);
                ALHelper.CheckError("Failed to pause source.");
            }
            ++pauseCount;
            SoundState = SoundState.Paused;
        }

        private void PlatformPlay()
        {
            if (_effect.SoundBufferStreamed == null)
                PlayMemoryResident();
            else
                PlayStreamed();
        }

        const int MAX_BUFFERS = 5;
        const long BUFFER_FILL_SZ = 131072;

        private void PlayStreamed()
        {
            //TODO:: Setup Source
            currentBufferPosition = 0;

            SourceId = 0;
            HasSourceId = false;
            SourceId = controller.ReserveSource();
            HasSourceId = true;
            ALHelper.CheckError("Failed to reserve source.");

            buffers = AL.GenBuffers(MAX_BUFFERS);
            ALHelper.CheckError("Failed to reserve buffers to source.");

            AL.Source(SourceId, ALSourcei.Buffer, 0);

            foreach (var buffer in buffers) {
                if (_effect.SoundBufferStreamed.alignment > 0) {
                    AL.Bufferi (buffer, ALBufferi.UnpackBlockAlignmentSoft, _effect.SoundBufferStreamed.alignment);
                    ALHelper.CheckError("Failed to set buffer block alignment.");
                }
            }

            // Send the position, gain, looping, pitch, and distance model to the OpenAL driver.
            if (!HasSourceId)
				return;

			// Distance Model
			AL.DistanceModel (ALDistanceModel.InverseDistanceClamped);
            ALHelper.CheckError("Failed set source distance.");
			// Pan
			AL.Source (SourceId, ALSource3f.Position, _pan, 0, 0.1f);
            ALHelper.CheckError("Failed to set source pan.");
			// Volume
			AL.Source (SourceId, ALSourcef.Gain, _alVolume);
            ALHelper.CheckError("Failed to set source volume.");
			// Looping - unecessary for streamed data.
			// AL.Source (SourceId, ALSourceb.Looping, IsLooped);
            // ALHelper.CheckError("Failed to set source loop state.");
			// Pitch
			AL.Source (SourceId, ALSourcef.Pitch, XnaPitchToAlPitch(_pitch));
            ALHelper.CheckError("Failed to set source pitch.");

#if SUPPORTS_EFX
            ApplyReverb ();
            ApplyFilter ();
#endif

            PushIfNeeded(buffers);
            AL.SourcePlay(SourceId);
            
            SoundState = SoundState.Playing;
        }

        private void PlayMemoryResident()
        {
            SourceId = 0;
            HasSourceId = false;
            SourceId = controller.ReserveSource();
            HasSourceId = true;

            int bufferId = _effect.SoundBuffer.OpenALDataBuffer;
            AL.Source(SourceId, ALSourcei.Buffer, bufferId);
            ALHelper.CheckError("Failed to bind buffer to source.");

            // Send the position, gain, looping, pitch, and distance model to the OpenAL driver.
            if (!HasSourceId)
				return;

			// Distance Model
			AL.DistanceModel (ALDistanceModel.InverseDistanceClamped);
            ALHelper.CheckError("Failed set source distance.");
			// Pan
			AL.Source (SourceId, ALSource3f.Position, _pan, 0, 0.1f);
            ALHelper.CheckError("Failed to set source pan.");
			// Volume
			AL.Source (SourceId, ALSourcef.Gain, _alVolume);
            ALHelper.CheckError("Failed to set source volume.");
			// Looping
			AL.Source (SourceId, ALSourceb.Looping, IsLooped);
            ALHelper.CheckError("Failed to set source loop state.");
			// Pitch
			AL.Source (SourceId, ALSourcef.Pitch, XnaPitchToAlPitch(_pitch));
            ALHelper.CheckError("Failed to set source pitch.");
#if SUPPORTS_EFX
            ApplyReverb ();
            ApplyFilter ();
#endif
            AL.SourcePlay(SourceId);
            ALHelper.CheckError("Failed to play source.");


            SoundState = SoundState.Playing;
        }

        public void PushIfNeeded()
        {
            if (State != SoundState.Playing || _effect.SoundBufferStreamed == null)
                return;

            int buffersProcessed = 0;
            AL.GetSource(SourceId, ALGetSourcei.BuffersProcessed, out buffersProcessed);
            ALHelper.CheckError("Failed to get processed buffer count.");
            if (buffersProcessed <= 0)
                return;

            int[] buffers = AL.SourceUnqueueBuffers(SourceId, buffersProcessed);
            ALHelper.CheckError("Failed to unqueue buffers.");

            PushIfNeeded(buffers);
        }

        public void PushIfNeeded(int[] buffers)
        {
            if (_effect.SoundBufferStreamed == null)
                return;

            ref OALSoundBufferStreamed stream = ref _effect.SoundBufferStreamed;

            int buffersQueued = 0;
            AL.GetSource(SourceId, ALGetSourcei.BuffersQueued, out buffersQueued);
            ALHelper.CheckError("Failed to get queued buffer count.");

            //If we have at least two buffers left, come back later
            if (buffersQueued > 2)
                return;

            if (buffersQueued == 0 && currentBufferPosition == stream.size && _looped == false) {
                PlatformStop(true);
                HasSourceId = false;
            }

            // Accesses to data must be made aligned to one "unpack unit" - these formulas calculate how big such unit is
            // See: https://github.com/kcat/openal-soft/blob/5eb93f6c7437a7b08f500a2484f9734499f36976/al/buffer.cpp#L544
            int align = 0;
            if (stream.format == ALFormat.MonoMSADPCM || stream.format == ALFormat.StereoMSADPCM)
            {
                align = (int)stream.channels * ((stream.alignment-2)/2 + 7);
            }
            else
            {
                int bytesize = 
                    (stream.format == ALFormat.Mono8 || stream.format == ALFormat.Stereo8) ? 1 :
                    (stream.format == ALFormat.Mono16 || stream.format == ALFormat.Stereo16) ? 2 :
                    4;

                align = (int)stream.channels * bytesize * stream.alignment;
            }

            foreach (var buffer in buffers) {
                long sz = BUFFER_FILL_SZ;
                long left_in_stream = stream.size - currentBufferPosition;
                
                sz = (left_in_stream < sz) ? left_in_stream : sz;
                sz = sz - (sz % align);

                if (sz <= 0)
                {
                    if (!_looped)
                        break;
                    
                    currentBufferPosition = 0;
                    sz = (stream.size < BUFFER_FILL_SZ) ? stream.size : BUFFER_FILL_SZ;
                    sz = sz - (sz % align);
                }

                // System.Console.WriteLine("sz: {0}, align: {1}, remainder: {2}", sz, align, sz%align);
                AL.BufferData((uint)buffer, (int)stream.format, IntPtr.Add(stream.dataBuffer, (int)currentBufferPosition), (int)sz, stream.sampleRate);
                ALHelper.CheckError("Failed to push data to buffer.");

                AL.SourceQueueBuffer(SourceId, buffer);
                ALHelper.CheckError("Failed to queue buffer.");

                currentBufferPosition += sz;
            }
        }

        private void PlatformResume()
        {
            if (!HasSourceId)
            {
                Play();
                return;
            }

            if (SoundState == SoundState.Paused)
            {
                if (!controller.CheckInitState())
                {
                    return;
                }
                --pauseCount;
                if (pauseCount == 0)
                {
                    AL.SourcePlay(SourceId);
                    ALHelper.CheckError("Failed to play source.");
                }
            }
            SoundState = SoundState.Playing;
        }

        private void PlatformStop(bool immediate)
        {
            if (HasSourceId)
            {
                if (!controller.CheckInitState())
                {
                    return;
                }
                AL.SourceStop(SourceId);
                ALHelper.CheckError("Failed to stop source.");

#if SUPPORTS_EFX
                // Reset the SendFilter to 0 if we are NOT using revert since 
                // sources are recyled
                OpenALSoundController.Efx.BindSourceToAuxiliarySlot (SourceId, 0, 0, 0);
                ALHelper.CheckError ("Failed to unset reverb.");
                AL.Source (SourceId, ALSourcei.EfxDirectFilter, 0);
                ALHelper.CheckError ("Failed to unset filter.");
#endif
                AL.Source(SourceId, ALSourcei.Buffer, 0);
                ALHelper.CheckError("Failed to free source from buffer.");

                AL.DeleteBuffers(buffers);
                ALHelper.CheckError("Failed to free one or more buffers.");

                controller.FreeSource(this);
            }
            SoundState = SoundState.Stopped;
        }

        private void PlatformSetIsLooped(bool value)
        {
            _looped = value;

            if (HasSourceId)
            {
                AL.Source(SourceId, ALSourceb.Looping, _looped);
                ALHelper.CheckError("Failed to set source loop state.");
            }
        }

        private bool PlatformGetIsLooped()
        {
            return _looped;
        }

        private void PlatformSetPan(float value)
        {
            if (HasSourceId)
            {
                AL.Source(SourceId, ALSource3f.Position, value, 0.0f, 0.1f);
                ALHelper.CheckError("Failed to set source pan.");
            }
        }

        private void PlatformSetPitch(float value)
        {
            if (HasSourceId)
            {
                AL.Source(SourceId, ALSourcef.Pitch, XnaPitchToAlPitch(value));
                ALHelper.CheckError("Failed to set source pitch.");
            }
        }

        private SoundState PlatformGetState()
        {
            if (!HasSourceId)
                return SoundState.Stopped;
            
            var alState = AL.GetSourceState(SourceId);
            ALHelper.CheckError("Failed to get source state.");

            switch (alState)
            {
                case ALSourceState.Initial:
                case ALSourceState.Stopped:
                    SoundState = SoundState.Stopped;
                    break;

                case ALSourceState.Paused:
                    SoundState = SoundState.Paused;
                    break;

                case ALSourceState.Playing:
                    SoundState = SoundState.Playing;
                    break;
            }

            return SoundState;
        }

        private void PlatformSetVolume(float value)
        {
            _alVolume = value;

            if (HasSourceId)
            {
                AL.Source(SourceId, ALSourcef.Gain, _alVolume);
                ALHelper.CheckError("Failed to set source volume.");
            }
        }

        internal void PlatformSetReverbMix(float mix)
        {
#if SUPPORTS_EFX
            if (!OpenALSoundController.Efx.IsInitialized)
                return;
            reverb = mix;
            if (State == SoundState.Playing) {
                ApplyReverb ();
                reverb = 0f;
            }
#endif
        }

#if SUPPORTS_EFX
        void ApplyReverb ()
        {
            if (reverb > 0f && SoundEffect.ReverbSlot != 0) {
                OpenALSoundController.Efx.BindSourceToAuxiliarySlot (SourceId, (int)SoundEffect.ReverbSlot, 0, 0);
                ALHelper.CheckError ("Failed to set reverb.");
            }
        }

        void ApplyFilter ()
        {
            if (applyFilter && controller.Filter > 0) {
                var freq = frequency / 20000f;
                var lf = 1.0f - freq;
                var efx = OpenALSoundController.Efx;
                efx.Filter (controller.Filter, EfxFilteri.FilterType, (int)filterType);
                ALHelper.CheckError ("Failed to set filter.");
                switch (filterType) {
                case EfxFilterType.Lowpass:
                    efx.Filter (controller.Filter, EfxFilterf.LowpassGainHF, freq);
                    ALHelper.CheckError ("Failed to set LowpassGainHF.");
                    break;
                case EfxFilterType.Highpass:
                    efx.Filter (controller.Filter, EfxFilterf.HighpassGainLF, freq);
                    ALHelper.CheckError ("Failed to set HighpassGainLF.");
                    break;
                case EfxFilterType.Bandpass:
                    efx.Filter (controller.Filter, EfxFilterf.BandpassGainHF, freq);
                    ALHelper.CheckError ("Failed to set BandpassGainHF.");
                    efx.Filter (controller.Filter, EfxFilterf.BandpassGainLF, lf);
                    ALHelper.CheckError ("Failed to set BandpassGainLF.");
                    break;
                }
                AL.Source (SourceId, ALSourcei.EfxDirectFilter, controller.Filter);
                ALHelper.CheckError ("Failed to set DirectFilter.");
            }
        }
#endif

        internal void PlatformSetFilter(FilterMode mode, float filterQ, float frequency)
        {
#if SUPPORTS_EFX
            if (!OpenALSoundController.Efx.IsInitialized)
                return;

            applyFilter = true;
            switch (mode) {
            case FilterMode.BandPass:
                filterType = EfxFilterType.Bandpass;
                break;
                case FilterMode.LowPass:
                filterType = EfxFilterType.Lowpass;
                break;
                case FilterMode.HighPass:
                filterType = EfxFilterType.Highpass;
                break;
            }
            this.filterQ = filterQ;
            this.frequency = frequency;
            if (State == SoundState.Playing) {
                ApplyFilter ();
                applyFilter = false;
            }
#endif
        }

        internal void PlatformClearFilter()
        {
#if SUPPORTS_EFX
            if (!OpenALSoundController.Efx.IsInitialized)
                return;

            applyFilter = false;
#endif
        }

        private void PlatformDispose(bool disposing)
        {
            
        }
    }
}
