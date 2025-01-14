﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using ManagedCuda;
using ManagedCuda.BasicTypes;
using ManagedCuda.VectorTypes;
using Tracer.Classes.CUDA;
using Tracer.Classes.SceneObjects;
using Tracer.Interfaces;
using Tracer.Properties;
using Tracer.Structs;
using Tracer.Structs.CUDA;
using Tracer.TracerEventArgs;
using Tracer.Utilities;
using SceneCUDAData = System.Tuple<Tracer.Structs.CUDA.CUDAObject [ ], uint [ ]>;
using Timer = Utilib.Diagnostics.Timer;

namespace Tracer.Renderers
{
    public class CUDARenderer : IRenderer, IDisposable
    {
        public event EventHandler<RendererFinishedEventArgs> OnFinished;
        public event EventHandler<RenderSampleEventArgs> OnSampleFinished;

        private Thread RenderThread;

        public static string Path
        {
            get { return Environment.CurrentDirectory + "\\kernel.ptx"; }
        }

        private static readonly Random RNG = new Random( );

        private CudaKernel RenderKernel;
        private const int ThreadsPerBlock = 32;
        private bool CancelThread;
        private Scene Scn;
        private Bitmap Image;
        private Timer Timer;
        private uint TotalSamples;
        private uint Samples;
        private DateTime Start;
        private bool SkipToNextArea;

        #region Initialization

        private void InitKernels( )
        {
            CudaContext cntxt = new CudaContext( );
            //Add an info and error buffer to see what the linker wants to tell us:
            CudaJitOptionCollection options = new CudaJitOptionCollection( );
            CudaJOErrorLogBuffer err = new CudaJOErrorLogBuffer( 1024 );
            CudaJOInfoLogBuffer info = new CudaJOInfoLogBuffer( 1024 );
            options.Add( new CudaJOLogVerbose( true ) );
            options.Add( err );
            options.Add( info );
            try
            {
                CudaLinker linker = new CudaLinker( options );
                linker.AddFile( @"kernel.ptx", CUJITInputType.PTX, null );
                linker.AddFile( @"Material.ptx", CUJITInputType.PTX, null );
                linker.AddFile( @"VectorMath.ptx", CUJITInputType.PTX, null );

                //important: add the device runtime library!
                linker.AddFile( @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v6.0\lib\Win32\cudadevrt.lib",
                    CUJITInputType.Library, null );
                byte [ ] tempArray = linker.Complete( );
                //MessageBox.Show( info.Value );

                RenderKernel = cntxt.LoadKernelPTX( tempArray, "TraceKernelRegion" );
                RenderKernel.BlockDimensions = new dim3( ThreadsPerBlock, ThreadsPerBlock );
            }

            catch ( Exception E )
            {
                Console.WriteLine( E.Message );
            }
        }

        #endregion

        public void RenderImage( ref RenderSettings RenderSetting, Scene Scn )
        {
            this.Scn = Scn;
            int W = ( int ) Scn.Camera.Resolution.X;
            int H = ( int ) Scn.Camera.Resolution.Y;
            Image = new Bitmap( W, H, PixelFormat.Format32bppArgb );
            Start = DateTime.Now;

            // Item1 = objects, Item2 = lights
            Output.WriteLine( "Building scene." );
            SceneCUDAData Objs = Scn.ToCUDA( );

            uint XDivide = RenderSetting.AreaDivider;
            uint YDivide = RenderSetting.AreaDivider;

            int DivW = ( int ) ( W / XDivide );
            int DivH = ( int ) ( H / YDivide );
            
            this.Samples = 0;
            TotalSamples = ( XDivide ) * ( YDivide ) * RenderSetting.Samples;

            if ( Objs.Item2.Length <= 0 )
            {
                using ( Graphics G = Graphics.FromImage( Image ) )
                {
                    G.DrawString( "No lights in the scene.", new Font( new Font( "Arial", 12f ), FontStyle.Bold ),
                        new SolidBrush( Color.Red ), 0, 0 );
                }
                OnRenderFinished( );
                return;
            }

            if ( Objs.Item1.Length <= 0 )
            {
                using ( Graphics G = Graphics.FromImage( Image ) )
                {
                    G.DrawString( "No objects in the scene.", new Font( new Font( "Arial", 12f ), FontStyle.Bold ),
                        new SolidBrush( Color.Red ), 0, 0 );
                }
                OnRenderFinished( );
                return;
            }

            CudaDeviceVariable<CUDAObject> Obj = new CudaDeviceVariable<CUDAObject>( Objs.Item1.Length );
            Obj.CopyToDevice( Objs.Item1 );

            CudaDeviceVariable<uint> Lights = new CudaDeviceVariable<uint>( Objs.Item2.Length );
            Lights.CopyToDevice( Objs.Item2 );

            RenderKernel.SetConstantVariable( "ObjectArray", Obj.DevicePointer );
            RenderKernel.SetConstantVariable( "Objects", ( uint ) Objs.Item1.Length );
            RenderKernel.SetConstantVariable( "Lights", Lights.DevicePointer );
            RenderKernel.SetConstantVariable( "LightCount", ( uint ) Objs.Item2.Length );
            RenderKernel.SetConstantVariable( "Camera", Scn.Camera.ToCamData( ) );
            RenderKernel.SetConstantVariable( "MaxDepth", RenderSetting.MaxDepth );

            long Seed = RNG.Next( 0, Int32.MaxValue );
            Timer = new Timer( );

            Output.WriteLine( "Starting render." );
            // init parameters
            using ( CudaDeviceVariable<float3> CUDAVar_Output = new CudaDeviceVariable<float3>( DivW * DivH ) )
            {
                // run cuda method
                using ( CudaDeviceVariable<float3> CUDAVar_Input = new CudaDeviceVariable<float3>( DivW * DivH ) )
                {
                    float3 [ ] In = new float3[ DivW * DivH ];
                    CUDAVar_Input.CopyToDevice( In );
                    CUDAVar_Output.CopyToDevice( In );

                    for ( int X = 0; X < XDivide; X++ )
                    {
                        if ( CancelThread )
                            break;

                        for ( int Y = 0; Y < YDivide; Y++ )
                        {
                            if ( CancelThread )
                                break;

                            RenderRegion( ref RenderSetting, ref Seed, CUDAVar_Input, CUDAVar_Output, X * DivW, Y * DivH,
                                DivW,
                                DivH );
                            Seed += DivW * DivH;
                        }
                    }
                }
            }

            Obj.Dispose( );
            Lights.Dispose( );

            OnRenderFinished( );
        }

        private void OnRenderFinished( )
        {
            if ( OnFinished != null )
                OnFinished.Invoke( null, new RendererFinishedEventArgs
                {
                    AverageProgressTime = new TimeSpan( ( DateTime.Now - Start ).Ticks / TotalSamples ),
                    Time = DateTime.Now - Start,
                    Image = Image
                } );

            if ( CancelThread )
                CancelThread = false;
        }

        private void RenderRegion( ref RenderSettings RenderSetting, ref long Seed, CudaDeviceVariable<float3> Input,
            CudaDeviceVariable<float3> Output, int StartX, int StartY, int W, int H )
        {
            if ( StartX >= Scn.Camera.Resolution.X || StartY >= Scn.Camera.Resolution.Y )
                return;

            RenderKernel.GridDimensions = new dim3( W / ThreadsPerBlock + 1, H / ThreadsPerBlock + 1 );

            CUdeviceptr InPTR = Input.DevicePointer;

            uint Q = 0;
            while( Q < RenderSetting.Samples )
            {
                if ( CancelThread )
                    return;

                uint SamplesToRender = Math.Min( RenderSetting.SamplesPerRender, RenderSetting.Samples - Q );

                if ( SkipToNextArea )
                {
                    this.Samples += RenderSetting.Samples - Q;
                    SkipToNextArea = false;
                    return;
                }

                Timer.StartRound( );

                RenderKernel.Run( SamplesToRender, Seed, InPTR, StartX, StartY, W, H,
                    Output.DevicePointer );

                this.Samples += SamplesToRender;
                Seed += ( W * H ) * SamplesToRender;

                float3 [ ] Data = new float3[ W * H ];
                Output.CopyToHost( Data );
                
                Q += SamplesToRender;

                Bitmap OldImage = Image;
                Image = Utilities.Image.FillBitmapArea( Image, Q, StartX, StartY, W, H, Data );
                OldImage.Dispose( );

                TimeSpan RenderTime = Timer.StopRound( );

                if ( OnSampleFinished != null )
                    OnSampleFinished( this, new RenderSampleEventArgs
                    {
                        Data = Data,
                        AreaSampleCount = ( int ) Q,
                        TotalAreaSamples = ( int ) RenderSetting.Samples,
                        AverageSampleTime = new TimeSpan( Timer.Average.Ticks / SamplesToRender ),
                        TotalSamples = ( int ) TotalSamples,
                        Progress = ( float ) this.Samples / TotalSamples,
                        Time = RenderTime,
                        Image = Image
                    } );

                InPTR = Output.DevicePointer;
            }
        }

        public void Cancel( )
        {
            //RenderThread.Abort( );
            CancelThread = true;
        }

        public void NextArea( )
        {
            SkipToNextArea = true;
        }

        public void Run( )
        {
            RenderThread = new Thread( ( ) =>
            {
                InitKernels( );
                RenderSettings Setting = new RenderSettings
                {
                    AreaDivider = Settings.Default.Render_AreaDivider,
                    Samples = Settings.Default.Render_Samples,
                    MaxDepth = Settings.Default.Render_MaxDepth,
                    SamplesPerRender = Settings.Default.Render_SamplesPerRender
                };
                RenderImage( ref Setting, Renderer.Scene );
            } );

            RenderThread.Start( );
        }

        public List<IDevice> GetDevices( )
        {
            List<IDevice> Devices = new List<IDevice>( );
            for ( int X = 0; X < CudaContext.GetDeviceCount( ); X++ )
            {
                Devices.Add( new CUDADevice { Device = CudaContext.GetDeviceInfo( X ) } );
            }

            return Devices;
        }

        public void Dispose( )
        {
            Image.Dispose( );
        }
    }
}