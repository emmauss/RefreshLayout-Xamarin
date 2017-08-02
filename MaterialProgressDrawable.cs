using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Graphics;
using Android.Annotation;
using Android.Support.V4.View;
using Android.Support.V4.Animation;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Support.V4.View.Animation;
using Android.Widget;
using Java.Lang.Annotation;
using Java.Lang;

namespace RefreshLayout
{
    public class  MaterialProgressDrawable :Drawable, IAnimatable
    {
        private static readonly IInterpolator LINEAR_INTERPOLATOR = new LinearInterpolator();
        public static readonly IInterpolator MATERIAL_INTERPOLATOR = new FastOutSlowInInterpolator();

        private static readonly float FULL_ROTATION = 1080.0f;

        //@Retention(RetentionPolicy.SOURCE)
        //@IntDef({ LARGE, DEFAULT})
        public interface ProgressDrawableSize { }

        // Maps to ProgressBar.Large style
        public static readonly int LARGE = 0;
        // Maps to ProgressBar default style
        public static readonly int DEFAULT = 1;

        // Maps to ProgressBar default style
        private static readonly int CIRCLE_DIAMETER = 40;
        private static readonly float CENTER_RADIUS = 8.75f; //should add up to 10 when + stroke_width
        private static readonly float STROKE_WIDTH = 2.5f;

        // Maps to ProgressBar.Large style
        private static readonly int CIRCLE_DIAMETER_LARGE = 56;
        private static readonly float CENTER_RADIUS_LARGE = 12.5f;
        private static readonly float STROKE_WIDTH_LARGE = 3f;

        private static readonly int[] COLORS = new int[] {
        Color.Black
    };

        /**
         * The value in the linear interpolator for animating the drawable at which
         * the color transition should start
         */
        private static readonly float COLOR_START_DELAY_OFFSET = 0.75f;
        private static readonly float END_TRIM_START_DELAY_OFFSET = 0.5f;
        private static readonly float START_TRIM_DURATION_OFFSET = 0.5f;

        /** The duration of a single progress spin in milliseconds. */
        private static readonly int ANIMATION_DURATION = 1332;

        /** The number of points in the progress "star". */
        private static readonly float NUM_POINTS = 5f;
        /** The list of animators operating on this drawable. */
        private readonly List<Animation> mAnimators = new List<Animation>();

        /** The indicator ring, used to manage animation state. */
        private readonly Ring mRing;

        /** Canvas rotation in degrees. */
        private float mRotation;

        /** Layout info for the arrowhead in dp */
        private static readonly int ARROW_WIDTH = 10;
        private static readonly int ARROW_HEIGHT = 5;
        private static readonly float ARROW_OFFSET_ANGLE = 5;

        /** Layout info for the arrowhead for the large spinner in dp */
        private static readonly int ARROW_WIDTH_LARGE = 12;
        private static readonly int ARROW_HEIGHT_LARGE = 6;
        private static readonly float MAX_PROGRESS_ARC = .8f;

        private Resources mResources;
        private View mParent;
        private Animation mAnimation;
        float mRotationCount;
        private double mWidth;
        private double mHeight;
        bool mFinishing;

        public MaterialProgressDrawable(Context context, View parent)
        {
            mParent = parent;
            mResources = context.Resources;
            mCallback = new Callback(this);
            mRing = new Ring(mCallback);
            mRing.SetColors(COLORS);

            UpdateSizes(DEFAULT);
            SetupAnimators();
        }

        private void setSizeParameters(double progressCircleWidth, double progressCircleHeight,
                double centerRadius, double strokeWidth, float arrowWidth, float arrowHeight)
        {
            Ring ring = mRing;
            DisplayMetrics metrics = mResources.DisplayMetrics;
            float screenDensity = metrics.Density;

            mWidth = progressCircleWidth * screenDensity;
            mHeight = progressCircleHeight * screenDensity;
            ring.StrokeWidth = (float)(strokeWidth * screenDensity);
            ring.CenterRadius =centerRadius * screenDensity;
            ring.SetColorIndex(0);
            ring.setArrowDimensions(arrowWidth * screenDensity, arrowHeight * screenDensity);
            ring.SetInsets((int)mWidth, (int)mHeight);
        }

        /**
         * Set the overall size for the progress spinner. This updates the radius
         * and stroke width of the ring.
         *
         * @param size One of {@link MaterialProgressDrawable.LARGE} or
         *            {@link MaterialProgressDrawable.DEFAULT}
         */
        public void UpdateSizes(int size)
        {
            if (size == LARGE)
            {
                setSizeParameters(CIRCLE_DIAMETER_LARGE, CIRCLE_DIAMETER_LARGE, CENTER_RADIUS_LARGE,
                        STROKE_WIDTH_LARGE, ARROW_WIDTH_LARGE, ARROW_HEIGHT_LARGE);
            }
            else
            {
                setSizeParameters(CIRCLE_DIAMETER, CIRCLE_DIAMETER, CENTER_RADIUS, STROKE_WIDTH,
                        ARROW_WIDTH, ARROW_HEIGHT);
            }
        }

        /**
         * @param show Set to true to display the arrowhead on the progress spinner.
         */
        public void showArrow(bool show)
        {
            mRing.ShowArrow =show;
        }

        /**
         * @param scale Set the scale of the arrowhead for the spinner.
         */
        public void setArrowScale(float scale)
        {
            mRing.ArrowScale=scale;
        }

        /**
         * Set the start and end trim for the progress spinner arc.
         *
         * @param startAngle start angle
         * @param endAngle end angle
         */
        public void setStartEndTrim(float startAngle, float endAngle)
        {
            mRing.StartTrim =startAngle;
            mRing.EndTrim =endAngle;
        }

        /**
         * Set the amount of rotation to apply to the progress spinner.
         *
         * @param rotation Rotation is from [0..1]
         */
        public void setProgressRotation(float rotation)
        {
            mRing.Rotation =rotation;
        }

        /**
         * Update the background color of the circle image view.
         */
        public void SetBackgroundColor(int color)
        {
            mRing.setBackgroundColor(color);
        }

        /**
         * Set the colors used in the progress animation from color resources.
         * The first color will also be the color of the bar that grows in response
         * to a user swipe gesture.
         *
         * @param colors
         */
        public void SetColorSchemeColors(int[] colors)
        {
            mRing.SetColors(colors);
            mRing.SetColorIndex(0);
        }

        public override int IntrinsicHeight
        {
            get
            {
                return (int)mHeight;
            }
        }

        public override int IntrinsicWidth
        {
            get
            {
                return (int)mWidth;
            }
        }




        public override int Opacity
        {
            get
            {
                return (int)Format.Transparent;
            }
        }



        public bool IsRunning
        {
            get
            {
                List<Animation> animators = mAnimators;
                int N = animators.Count;
                for (int i = 0; i < N; i++)
                {
                    Animation animator = animators[i];
                    if (animator.HasStarted && !animator.HasEnded)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public override void Draw(Canvas canvas)
        {
            Rect bounds = Bounds;
            int saveCount = canvas.Save();
            canvas.Rotate(mRotation, bounds.ExactCenterX(), bounds.ExactCenterY());
            mRing.Draw(canvas, bounds);
            canvas.RestoreToCount(saveCount);
        }

        public override void SetAlpha(int alpha)
        {
            Alpha = alpha;
        }

        public override int Alpha
        {
            get => mRing.Alpha;
            set => mRing.Alpha = value;
        }

        public override void SetColorFilter(ColorFilter colorFilter)
        {
            mRing.ColorFilter = colorFilter;
        }

        float Rotation
        {
            set
            {
                mRotation = value;
                InvalidateSelf();
            }
        }

        float GetMinProgressArc(Ring ring)
        {

            return (float)ToRadians(
                    ring.StrokeWidth / (2 * System.Math.PI * ring.CenterRadius));
        }

        public static double ToRadians( double angleIn10thofaDegree)
        {
            // Angle in 10th of a degree
            return (angleIn10thofaDegree * System.Math.PI) / 1800;

        }

        private float getRotation()
        {
            return mRotation;
        }



        public void Start()
        {
            mAnimation.Reset();
            mRing.StoreOriginals();
            // Already showing some part of the ring
            if (mRing.EndTrim != mRing.StartTrim)
            {
                mFinishing = true;
                mAnimation.Duration = ANIMATION_DURATION / 2;
                mParent.StartAnimation(mAnimation);
            }
            else
            {
                mRing.SetColorIndex(0);
                mRing.ResetOriginals();
                mAnimation.Duration = ANIMATION_DURATION;
                mParent.StartAnimation(mAnimation);
            }
        }

        public void Stop()
        {
            mParent.ClearAnimation();
            Rotation = 0;
            mRing.ShowArrow = false;
            mRing.SetColorIndex(0);
            mRing.ResetOriginals();
        }

        // Adapted from ArgbEvaluator.java
        private int evaluateColorChange(float fraction, int startValue, int endValue)
        {
            int startInt = (int)startValue;
            int startA = (startInt >> 24) & 0xff;
            int startR = (startInt >> 16) & 0xff;
            int startG = (startInt >> 8) & 0xff;
            int startB = startInt & 0xff;

            int endInt = (int)endValue;
            int endA = (endInt >> 24) & 0xff;
            int endR = (endInt >> 16) & 0xff;
            int endG = (endInt >> 8) & 0xff;
            int endB = endInt & 0xff;

            return (int)((startA + (int)(fraction * (endA - startA))) << 24)
                    | (int)((startR + (int)(fraction * (endR - startR))) << 16)
                    | (int)((startG + (int)(fraction * (endG - startG))) << 8)
                    | (int)((startB + (int)(fraction * (endB - startB))));
        }

        /**
         * Update the ring color if this is within the last 25% of the animation.
         * The new ring color will be a translation from the starting ring color to
         * the next color.
         */
        void UpdateRingColor(float interpolatedTime, Ring ring)
        {
            if (interpolatedTime > COLOR_START_DELAY_OFFSET)
            {
                // scale the interpolatedTime so that the full
                // transformation from 0 - 1 takes place in the
                // remaining time
                ring.Color = new Color(evaluateColorChange((interpolatedTime - COLOR_START_DELAY_OFFSET)
                        / (1.0f - COLOR_START_DELAY_OFFSET), ring.StartingColor,
                        ring.NextColor));
            }
        }

        void ApplyFinishTranslation(float interpolatedTime, Ring ring)
        {
            // shrink back down and complete a full rotation before
            // starting other circles
            // Rotation goes between [0..1].
            UpdateRingColor(interpolatedTime, ring);
            float targetRotation = (float)(System.Math.Floor(ring.StartingRotation / MAX_PROGRESS_ARC)
                    + 1f);
            float minProgressArc = GetMinProgressArc(ring);
            float startTrim = ring.StartingStartTrim
                   + (ring.StartingEndTrim - minProgressArc - ring.StartingStartTrim)
                   * interpolatedTime;
            ring.StartTrim =startTrim;
            ring.EndTrim =ring.StartingEndTrim;
            float rotation = ring.StartingRotation
                   + ((targetRotation - ring.StartingRotation) * interpolatedTime);
            ring.Rotation =rotation;
        }

        public class MAnimation : Animation
        {
            MaterialProgressDrawable material;
            Ring ring;
            public MAnimation(MaterialProgressDrawable material, Ring ring)
            {
                this.ring = ring;
                this.material = material;
            }
            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                if (material.mFinishing)
                {
                    material.ApplyFinishTranslation(interpolatedTime, ring);
                }
                else
                {
                    // The minProgressArc is calculated from 0 to create an
                    // angle that matches the stroke width.
                    float minProgressArc = material.GetMinProgressArc(ring);
                    float startingEndTrim = ring.StartingEndTrim;
                    float startingTrim = ring.StartingStartTrim;
                    float startingRotation = ring.StartingRotation;

                    material.UpdateRingColor(interpolatedTime, ring);

                    // Moving the start trim only occurs in the first 50% of a
                    // single ring animation
                    if (interpolatedTime <= START_TRIM_DURATION_OFFSET)
                    {
                        // scale the interpolatedTime so that the full
                        // transformation from 0 - 1 takes place in the
                        // remaining time
                        float scaledTime = (interpolatedTime)
                               / (1.0f - START_TRIM_DURATION_OFFSET);
                        float startTrim = startingTrim
                               + ((MAX_PROGRESS_ARC - minProgressArc) * MATERIAL_INTERPOLATOR
                                       .GetInterpolation(scaledTime));
                        ring.StartTrim =startTrim;
                    }

                    // Moving the end trim starts after 50% of a single ring
                    // animation completes
                    if (interpolatedTime > END_TRIM_START_DELAY_OFFSET)
                    {
                        // scale the interpolatedTime so that the full
                        // transformation from 0 - 1 takes place in the
                        // remaining time
                        float minArc = MAX_PROGRESS_ARC - minProgressArc;
                        float scaledTime = (interpolatedTime - START_TRIM_DURATION_OFFSET)
                                / (1.0f - START_TRIM_DURATION_OFFSET);
                        float endTrim = startingEndTrim
                               + (minArc * MATERIAL_INTERPOLATOR.GetInterpolation(scaledTime));
                        ring.EndTrim = endTrim;
                    }

                    float rotation = startingRotation + (0.25f * interpolatedTime);
                    ring.Rotation =rotation;

                    float groupRotation = ((FULL_ROTATION / NUM_POINTS) * interpolatedTime)
                            + (FULL_ROTATION * (material.mRotationCount / NUM_POINTS));
                    material.Rotation =groupRotation;
                }
            }

        }

        public class AnimationListener : Java.Lang.Object, Animation.IAnimationListener
        {
            MaterialProgressDrawable material;
            Ring ring;
            public AnimationListener(MaterialProgressDrawable material, Ring ring)
            {
                this.ring = ring;
                this.material = material;
            }

            public void OnAnimationEnd(Animation animation)
            {
                throw new NotImplementedException();
            }

            public void OnAnimationRepeat(Animation animation)
            {
                ring.StoreOriginals();
                ring.GoToNextColor();
                ring.StartTrim =ring.EndTrim;
                if (material.mFinishing)
                {
                    // finished closing the last ring from the swipe gesture; go
                    // into progress mode
                    material.mFinishing = false;
                    animation.Duration = ANIMATION_DURATION;
                    ring.ShowArrow=false;
                }
                else
                {
                    material.mRotationCount = (material.mRotationCount + 1) % (NUM_POINTS);
                }
            }

            public void OnAnimationStart(Animation animation)
            {
                material.mRotationCount = 0;
            }
        }


        private void SetupAnimators()
        {
            Ring ring = mRing;
            Animation animation = new MAnimation(this,mRing);


            animation.RepeatCount = Animation.Infinite;
            animation.RepeatMode = RepeatMode.Restart;
            animation.Interpolator = LINEAR_INTERPOLATOR;
            animation.SetAnimationListener(new AnimationListener(this,ring));
            mAnimation = animation;
        }

        private Callback mCallback;

        public class Callback : Java.Lang.Object, ICallback
        {
            MaterialProgressDrawable material;
            public Callback(MaterialProgressDrawable material)
            {
              
                this.material = material;
            }

            public void InvalidateDrawable(Drawable who)
            {
                material.InvalidateSelf();
            }

            public void ScheduleDrawable(Drawable who, IRunnable what, long when)
            {
                material.ScheduleSelf(what, when);
            }

            public void UnscheduleDrawable(Drawable who, IRunnable what)
            {
                material.UnscheduleSelf(what);
            }
        }
        


        public class Ring
        {
            private RectF mTempBounds = new RectF();
            private Paint mPaint = new Paint();
            private Paint mArrowPaint = new Paint();

            private Callback mCallback;

            private float mStartTrim = 0.0f;
            private float mEndTrim = 0.0f;
            private float mRotation = 0.0f;
            private float mStrokeWidth = 5.0f;
            private float mStrokeInset = 2.5f;

            private int[] mColors;
            // mColorIndex represents the offset into the available mColors that the
            // progress circle should currently display. As the progress circle is
            // animating, the mColorIndex moves by one to the next available color.
            private int mColorIndex;
            private float mStartingStartTrim;
            private float mStartingEndTrim;
            private float mStartingRotation;
            private bool mShowArrow;
            private Path mArrow;
            private float mArrowScale;
            private double mRingCenterRadius;
            private int mArrowWidth;
            private int mArrowHeight;
            private int mAlpha;
            private Paint mCirclePaint = new Paint(PaintFlags.AntiAlias);
            private int mBackgroundColor;
            private int mCurrentColor;

            public Ring(Callback callback)
            {
                mCallback = callback;

                mPaint.StrokeCap = Paint.Cap.Square;
                mPaint.AntiAlias = true;
                mPaint.SetStyle(Paint.Style.Stroke);

                mArrowPaint.SetStyle(Paint.Style.Fill);
                mArrowPaint.AntiAlias = true;
            }

            public void setBackgroundColor(int color)
            {
                mBackgroundColor = color;
            }

            /**
             * Set the dimensions of the arrowhead.
             *
             * @param width Width of the hypotenuse of the arrow head
             * @param height Height of the arrow point
             */
            public void setArrowDimensions(float width, float height)
            {
                mArrowWidth = (int)width;
                mArrowHeight = (int)height;
            }

            /**
             * Draw the progress spinner
             */
            public void Draw(Canvas c, Rect bounds)
            {
                RectF arcBounds = mTempBounds;
                arcBounds.Set(bounds);
                arcBounds.Inset(mStrokeInset, mStrokeInset);

                float startAngle = (mStartTrim + mRotation) * 360;
                float endAngle = (mEndTrim + mRotation) * 360;
                float sweepAngle = endAngle - startAngle;

                mPaint.Color = new Color(mCurrentColor);
                c.DrawArc(arcBounds, startAngle, sweepAngle, false, mPaint);

                DrawTriangle(c, startAngle, sweepAngle, bounds);

                if (mAlpha < 255)
                {
                    mCirclePaint.Color = new Color(mBackgroundColor);
                    mCirclePaint.Alpha = (255 - mAlpha);
                    c.DrawCircle(bounds.ExactCenterX(), bounds.ExactCenterY(), bounds.Width() / 2,
                            mCirclePaint);
                }
            }

            private void DrawTriangle(Canvas c, float startAngle, float sweepAngle, Rect bounds)
            {
                if (mShowArrow)
                {
                    if (mArrow == null)
                    {
                        mArrow = new Android.Graphics.Path();
                        mArrow.SetFillType(Android.Graphics.Path.FillType.EvenOdd);
                    }
                    else
                    {
                        mArrow.Reset();
                    }

                    // Adjust the position of the triangle so that it is inset as
                    // much as the arc, but also centered on the arc.
                    float inset = (int)mStrokeInset / 2 * mArrowScale;
                    float x = (float)(mRingCenterRadius * System.Math.Cos(0) + bounds.ExactCenterX());
                    float y = (float)(mRingCenterRadius * System.Math.Sin(0) + bounds.ExactCenterY());

                    // Update the path each time. This works around an issue in SKIA
                    // where concatenating a rotation matrix to a scale matrix
                    // ignored a starting negative rotation. This appears to have
                    // been fixed as of API 21.
                    mArrow.MoveTo(0, 0);
                    mArrow.LineTo(mArrowWidth * mArrowScale, 0);
                    mArrow.LineTo((mArrowWidth * mArrowScale / 2), (mArrowHeight
                            * mArrowScale));
                    mArrow.Offset(x - inset, y);
                    mArrow.Close();
                    // draw a triangle
                    mArrowPaint.Color = new Color(mCurrentColor);
                    c.Rotate(startAngle + sweepAngle - ARROW_OFFSET_ANGLE, bounds.ExactCenterX(),
                            bounds.ExactCenterY());
                    c.DrawPath(mArrow, mArrowPaint);
                }
            }

            /**
             * Set the colors the progress spinner alternates between.
             *
             * @param colors Array of integers describing the colors. Must be non-<code>null</code>.
             */
            public void SetColors(int[] colors)
            {
                mColors = colors;
                // if colors are reset, make sure to reset the color index as well
                SetColorIndex(0);
            }

            /**
             * Set the absolute color of the progress spinner. This is should only
             * be used when animating between current and next color when the
             * spinner is rotating.
             *
             * @param color int describing the color.
             */
            public int Color
            {
                get
                {
                    return mCurrentColor;
                }
                set
                {
                    mCurrentColor = value;
                }
            }

            /**
             * @param index Index into the color array of the color to display in
             *            the progress spinner.
             */
            public void SetColorIndex(int index)
            {
                mColorIndex = index;
                mCurrentColor = mColors[mColorIndex];
            }

            /**
             * @return int describing the next color the progress spinner should use when drawing.
             */
            public int NextColor
            {
                get
                {
                    return mColors[NextColorIndex];
                }
            }

            private int NextColorIndex
            {
                get
                {
                    return (mColorIndex + 1) % (mColors.Length);
                }
            }

            /**
             * Proceed to the next available ring color. This will automatically
             * wrap back to the beginning of colors.
             */
            public void GoToNextColor()
            {
                SetColorIndex(NextColorIndex);
            }

            public ColorFilter ColorFilter
            {
                set
                {
                    mPaint.SetColorFilter(value);
                    InvalidateSelf();
                }
            }

            public int Alpha
            {
                get
                {
                    //Current alpha of the progress spinner and arrowhead.
                    return mAlpha;
                }
                set
                {
                    //Set the alpha of the progress spinner and associated arrowhead.
                    mAlpha = value;
                }
            }


            /**
             * @param strokeWidth Set the stroke width of the progress spinner in pixels.
             */
            public float StrokeWidth
            {
                set
                {
                    mStrokeWidth = value;
                    mPaint.StrokeWidth = value;
                    InvalidateSelf();
                }
                get
                {
                    return mStrokeWidth;
                }
            }



            public float StartTrim
            {
                get
                {
                    return mStartTrim;
                }
                set
                {
                    mStartTrim = value;
                    InvalidateSelf();
                }
            }

            public float EndTrim
            {
                get
                {
                    return mEndTrim;
                }
                set
                {
                    mEndTrim = value;
                    InvalidateSelf();
                }
            }


            public float StartingStartTrim
            {
                get
                {
                    return mStartingStartTrim;
                }
            }

            public float StartingEndTrim
            {
                get
                {
                    return mStartingEndTrim;
                }
            }

            public int StartingColor
            {
                get
                {
                    return mColors[mColorIndex];
                }
            }




            public float Rotation
            {
                get
                {
                    return mRotation;
                }
                set
                {
                    mRotation = value;
                    InvalidateSelf();
                }
            }



            public void SetInsets(int width, int height)
            {
                float minEdge = (float)System.Math.Min(width, height);
                float insets;
                if (mRingCenterRadius <= 0 || minEdge < 0)
                {
                    insets = (float)System.Math.Ceiling(mStrokeWidth / 2.0f);
                }
                else
                {
                    insets = (float)(minEdge / 2.0f - mRingCenterRadius);
                }
                mStrokeInset = insets;
            }


            public float GetInsets()
            {
                return mStrokeInset;
            }

            /**
             * @param mRIngcenterRadius Inner radius in px of the circle the progress
             *            spinner arc traces.
             */
            public double CenterRadius
            {
                get
                {
                    return mRingCenterRadius;
                }
                set
                {
                    mRingCenterRadius = value;
                }
            }


            /**
             * @param show Set to true to show the arrow head on the progress spinner.
             */
            public bool ShowArrow
            {
                set
                {
                    if (mShowArrow != value)
                    {
                        mShowArrow = value;
                        InvalidateSelf();
                    }
                }
            }

            /**
             * @param scale Set the scale of the arrowhead for the spinner.
             */
            public float ArrowScale
            {
                set
                {
                    if (value != mArrowScale)
                    {
                        mArrowScale = value;
                        InvalidateSelf();
                    }
                }
            }

            /**
             * @return The amount the progress spinner is currently rotated, between [0..1].
             */
            public float StartingRotation
            {
                get
                {
                    return mStartingRotation;
                }
            }

            /**
             * If the start / end trim are offset to begin with, store them so that
             * animation starts from that offset.
             */
            public void StoreOriginals()
            {
                mStartingStartTrim = mStartTrim;
                mStartingEndTrim = mEndTrim;
                mStartingRotation = mRotation;
            }

            /**
             * Reset the progress spinner to default rotation, start and end angles.
             */
            public void ResetOriginals()
            {
                mStartingStartTrim = 0;
                mStartingEndTrim = 0;
                mStartingRotation = 0;
                StartTrim = 0;
                EndTrim = 0;
                Rotation = 0;
            }

            private void InvalidateSelf()
            {
                mCallback.InvalidateDrawable(null);
            }
        }
    }
}