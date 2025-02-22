﻿using System;
using System.ComponentModel;
using Tracer.Utilities;

namespace Tracer.Classes.Util
{
    [TypeConverter( typeof ( ExpandableObjectConverter ) )]
    [Serializable]
    public class Angle
    {
        private bool RefreshForward = true, RefreshUp = true, RefreshRight = true;

        /// <summary>
        /// The pitch of this angle.
        /// </summary>
        public float Pitch
        {
            set
            {
                Pitch1 = value % 360f;
                PitchRadians = MathHelper.ToRadians( Pitch1 );
                RefreshForward = RefreshUp = RefreshRight = true;
            }
            get { return Pitch1; }
        }

        /// <summary>
        /// The yaw of this angle.
        /// </summary>
        public float Yaw
        {
            set
            {
                Yaw1 = value % 360f;
                YawRadians = MathHelper.ToRadians( Yaw1 );
                RefreshForward = RefreshUp = RefreshRight = true;
            }
            get { return Yaw1; }
        }

        /// <summary>
        /// The roll of this angle.
        /// </summary>
        public float Roll
        {
            set
            {
                Roll1 = value % 360f;
                RollRadians = MathHelper.ToRadians( Roll1 );
                RefreshForward = RefreshUp = RefreshRight = true;
            }
            get { return Roll1; }
        }

        private float Pitch1;
        private float Yaw1;
        private float Roll1;

        [Browsable( false )]
        public float PitchRadians { private set; get; }

        [Browsable( false )]
        public float YawRadians { private set; get; }

        [Browsable( false )]
        public float RollRadians { private set; get; }

        private Vector3 m_Forward, m_Up, m_Right;

        /// <summary>
        /// The forward vector of this angle.
        /// </summary>
        [Browsable( false )]
        public Vector3 Forward
        {
            get
            {
                if ( RefreshForward )
                {
                    float X = ( float ) ( Math.Sin( YawRadians ) * Math.Cos( PitchRadians ) );
                    float Y = ( float ) ( Math.Sin( PitchRadians ) );
                    float Z = ( float ) ( -Math.Cos( YawRadians ) * Math.Cos( PitchRadians ) );

                    m_Forward = new Vector3( X, Y, Z ).Normalized( );
                    RefreshForward = false;
                }

                return m_Forward;
            }
        }

        /// <summary>
        /// The up vector of this angle.
        /// </summary>
        [Browsable( false )]
        public Vector3 Up
        {
            get
            {
                if ( RefreshUp )
                {
                    float X = ( float ) ( Math.Sin( YawRadians ) * Math.Cos( MathHelper.ToRadians( Pitch + 90 ) ) );
                    float Y = ( float ) Math.Sin( MathHelper.ToRadians( Pitch + 90 ) );
                    float Z = ( float ) ( -Math.Cos( YawRadians ) * Math.Cos( MathHelper.ToRadians( Pitch + 90 ) ) );

                    m_Up = new Vector3( X, Y, Z ).Normalized( );
                    RefreshUp = false;
                }

                return m_Up;
            }
        }

        /// <summary>
        /// The right vector of this angle.
        /// </summary>
        [Browsable( false )]
        public Vector3 Right
        {
            get
            {
                if ( RefreshRight )
                {
                    m_Right = Forward.Cross( Up );
                    RefreshRight = false;
                }

                return m_Right;
            }
        }

        public override string ToString( )
        {
            return string.Format( "( {0}, {1}, {2} )", Pitch, Yaw, Roll );
        }
    }
}