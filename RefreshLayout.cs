using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Graphics.Drawables;
using Android.Widget;
using Android.Content.Res;
using Android.Support.V4.Widget;
using Android.Support.V4;
using Android.Support.V4.Content;
using Android.Support.V4.View;
using Android.Util;
using Android.Views.Animations;
using Android.Animation;
using Android.Graphics;
//using static Android.Views.Animations.Animation;
using static Java.Lang.Object;

namespace RefreshLayout
{
    public class RefreshLayout : ViewGroup
    {
        public event EventHandler OnHeaderRefresh;
        public event EventHandler OnFooterRefresh;

        // Maps to ProgressBar.Large style
        public static readonly int LARGE = MaterialProgressDrawable.LARGE;
        // Maps to ProgressBar default style
        public static readonly int DEFAULT = MaterialProgressDrawable.DEFAULT;


        static readonly int CIRCLE_DIAMETER = 40;


        static readonly int CIRCLE_DIAMETER_LARGE = 56;

        //private static readonly String LOG_TAG = RefreshLayout.GetOb;

        private static readonly int MAX_ALPHA = 255;
        private static readonly int STARTING_PROGRESS_ALPHA = (int)(.3f * MAX_ALPHA);

        private static readonly float DECELERATE_INTERPOLATION_FACTOR = 2f;
        private static readonly int INVALID_POINTER = -1;
        private static readonly float DRAG_RATE = .5f;

        // Max amount of circle that can be filled by progress during swipe gesture,
        // where 1.0 is a full circle
        private static readonly float MAX_PROGRESS_ANGLE = .8f;

        private static readonly int SCALE_DOWN_DURATION = 150;

        private static readonly int ALPHA_ANIMATION_DURATION = 300;

        private static readonly int ANIMATE_TO_TRIGGER_DURATION = 200;

        private static readonly int ANIMATE_TO_START_DURATION = 200;

        // Default background for the progress spinner
        private static readonly int CIRCLE_BG_LIGHT = unchecked((int)0xFFFAFAFA);
        // Default offset in dips from the top of the view to where the progress spinner should stop
        private static readonly int DEFAULT_CIRCLE_TARGET = 64;

        private View mTarget; // the target of the gesture
        IOnRefreshListener mListener;
        bool mHeaderRefreshing = false;
        private int mTouchSlop;
        private float mHeaderTotalDragDistance = -1;

        // If nested scrolling is enabled, the total amount that needed to be
        // consumed by this as the nested scrolling parent is used in place of the
        // overscroll determined by MOVE events in the onTouch handler
        private float mTotalUnconsumed;
        private readonly NestedScrollingParentHelper mNestedScrollingParentHelper;
        private readonly NestedScrollingChildHelper mNestedScrollingChildHelper;
        private readonly int[] mParentScrollConsumed = new int[2];
        private readonly int[] mParentOffsetInWindow = new int[2];
        private bool mNestedScrollInProgress;

        private int mMediumAnimationDuration;
        int mHeaderCurrentTargetOffsetTop;

        private float mInitialMotionY;
        private float mInitialDownY;
        private bool mIsHeaderBeingDragged;
        private int mActivePointerId = INVALID_POINTER;
        // Whether this item is scaled up rather than clipped
        public bool mHeaderScale;

        // Target is returning to its start offset because it was cancelled or a
        // refresh was triggered.
        private bool mReturningToStart;
        private readonly DecelerateInterpolator mDecelerateInterpolator;
        private static readonly int[] LAYOUT_ATTRS = new int[] {
        Android.Resource.Attribute.Enabled
    };

        CircleImageView mCircleView;
        private int mCircleViewIndex = -1;

        protected int mHeaderFrom;

        float mHeaderStartingScale;

        protected int mHeaderOriginalOffsetTop;

        int mHeaderSpinnerOffsetEnd;

        MaterialProgressDrawable mProgress;

        private Animation mHeaderScaleAnimation;

        private Animation mHeaderScaleDownAnimation;

        private Animation mHeaderAlphaStartAnimation;

        private Animation mHeaderAlphaMaxAnimation;

        private Animation mHeaderScaleDownToStartAnimation;

        bool mHeaderNotify;

        private int mCircleDiameter;

        // Whether the client has set a custom starting position;
        bool mHeaderUsingCustomStart;

        private OnChildScrollCallback mChildScrollCallback;

        public class AnimationListener : Java.Lang.Object, Animation.IAnimationListener
        {
            RefreshLayout layout;
            public AnimationListener(RefreshLayout refresh)
            {
                layout = refresh;
            }

            public void OnAnimationEnd(Animation animation)
            {
                if (layout.mHeaderRefreshing)
                {
                    // Make sure the progress view is fully visible
                    layout.mProgress.Alpha =MAX_ALPHA;
                    layout.mProgress.Start();
                    if (layout.mHeaderNotify)
                    {
                        if (layout.mListener != null)
                        {

                            if (layout.mListener != null)
                                layout.mListener.OnHeaderRefresh();

                        }
                        layout.OnHeaderRefresh(this, EventArgs.Empty);
                    }
                    layout.mHeaderCurrentTargetOffsetTop = layout.mCircleView.Top;
                }
                else
                {
                    layout.ResetHeader();
                }
            }

            public void OnAnimationRepeat(Animation animation)
            {

            }

            public void OnAnimationStart(Animation animation)
            {

            }
        }


        private static readonly long RETURN_TO_ORIGINAL_POSITION_TIMEOUT = 300;
        private static readonly float ACCELERATE_INTERPOLATION_FACTOR = 1.5f;
        private static readonly float PROGRESS_BAR_HEIGHT = 4;
        private static readonly float MAX_SWIPE_DISTANCE_FACTOR = .6f;
        private static readonly int REFRESH_TRIGGER_DISTANCE = 120;

        private SwipeProgressBar mProgressBar; //the thing that shows progress is going
        private bool mIsFooterBeingDragged;
        private int mFooterOriginalOffsetTop;
        private int mFooterFrom;
        private bool mFooterRefreshing = false;
        private float mFooterDistanceToTriggerSync = -1;
        private float mFooterFromPercentage = 0;
        private float mFooterCurrPercentage = 0;
        private int mProgressBarHeight;
        private int mFooterCurrentTargetOffsetTop;
        private readonly AccelerateInterpolator mAccelerateInterpolator;

        public class AnimationFooterStarttoFinishPosition : Animation
        {
            RefreshLayout layout;
            public AnimationFooterStarttoFinishPosition(RefreshLayout refresh)
            {
                layout = refresh;
            }
            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                int targetTop = 0;
                if (layout.mFooterFrom != layout.mFooterOriginalOffsetTop)
                {
                    targetTop = (layout.mFooterFrom + (int)((layout.mFooterOriginalOffsetTop - layout.mFooterFrom) * interpolatedTime));
                }
                int offset = targetTop - layout.mTarget.Top;
                int currentTop = layout.mTarget.Top;
                if (offset + currentTop > 0)
                {
                    offset = 0 - currentTop;
                }
                //setFooterTargetOffsetTopAndBottom(offset);
            }
        }



        public readonly AnimationFooterStarttoFinishPosition mAnimateFooterOffsetToStartPosition; 

        public class ShrinkTrigger : Animation
        {
            RefreshLayout layout;
            public ShrinkTrigger(RefreshLayout refresh)
            {
                layout = refresh;
            }

            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                float percent = layout.mFooterFromPercentage + ((0 - layout.mFooterFromPercentage) * interpolatedTime);
                layout.mProgressBar.TriggerPercentage = percent;
            }
        }

        public ShrinkTrigger mShrinkTrigger;

        public Animation.IAnimationListener mReturnToStartPositionListener;

       private ShrinkAnimationListener mShrinkAnimationListener;

        private class ShrinkAnimationListener : BaseAnimationListener
        {
            RefreshLayout layout;
            public ShrinkAnimationListener(RefreshLayout refresh)
            {
                layout = refresh;
            }

            public override void OnAnimationEnd(Animation animation)
            {
                layout.mFooterCurrPercentage = 0;
            }
        };

        public class ReturnToStartPositionListener: BaseAnimationListener
        {
            RefreshLayout layout;
            public ReturnToStartPositionListener(RefreshLayout refresh)
            {
                layout = refresh;
            }

            public override void OnAnimationEnd(Animation animation)
            {
                // Once the target content has returned to its start position, reset
                // the target offset to 0
                layout.mFooterCurrentTargetOffsetTop = 0;
            }
        }

        private ReturnToStartPosition mReturnToStartPosition;

        private class ReturnToStartPosition : Java.Lang.Object, Java.Lang.IRunnable
        {
            RefreshLayout layout;
            public ReturnToStartPosition(RefreshLayout refresh)
            {
                layout = refresh;
            }
            public void Run()
            {
                layout.mReturningToStart = true;
                layout.AnimateFooterOffsetToStartPosition(layout.mFooterCurrentTargetOffsetTop + layout.PaddingTop,
                        layout.mReturnToStartPositionListener);
            }
        }

        private Cancel mCancel;

        private class Cancel : Java.Lang.Object, Java.Lang.IRunnable
        {
            RefreshLayout layout;
            public Cancel(RefreshLayout refresh)
            {
                layout = refresh;
            }

            public void Run()
            {
                layout.mReturningToStart = true;
                // Timeout fired since the user last moved their finger; animate the
                // trigger to 0 and put the target back at its original position
                if (layout.mProgressBar != null)
                {
                    layout.mFooterFromPercentage = layout.mFooterCurrPercentage;
                    layout.mShrinkTrigger.Duration =layout.mMediumAnimationDuration;
                    layout.mShrinkTrigger.SetAnimationListener(layout.mShrinkAnimationListener);
                    layout.mShrinkTrigger.Reset();
                    layout.mShrinkTrigger.Interpolator = layout.mDecelerateInterpolator;
                    layout.StartAnimation(layout.mShrinkTrigger);
                }
                layout.AnimateFooterOffsetToStartPosition(layout.mFooterCurrentTargetOffsetTop + layout.PaddingTop,
                        layout.mReturnToStartPositionListener);
            }
        }

        private bool mEnableSwipeHeader = true;
        private bool mEnableSwipeFooter = true;

        void ResetHeader()
        {
            mCircleView.ClearAnimation();
            mProgress.Stop();
            mCircleView.Visibility = ViewStates.Gone;
            HeaderColorViewAlpha = MAX_ALPHA;
            // Return the circle to its start position
            if (mHeaderScale)
            {
                SetAnimationProgress(0 /* animation complete and view is hidden */);
            }
            else
            {
               SetHeaderTargetOffsetTopAndBottom(mHeaderOriginalOffsetTop - mHeaderCurrentTargetOffsetTop,
                        true /* requires update */);
            }
            mHeaderCurrentTargetOffsetTop = mCircleView.Top;
        }

        void ResetFooter()
        {
            RemoveCallbacks(mCancel);
            RemoveCallbacks(mReturnToStartPosition);
        }



        public override bool Enabled
        {
            set
            {
                base.Enabled = value;
                if (!value)
                {
                    ResetHeader();
                    ResetFooter();
                }
            }
        }


        protected override void OnDetachedFromWindow()
        {
            base.OnDetachedFromWindow();
            ResetHeader();
            ResetFooter();
        }

        

        public int HeaderColorViewAlpha
        {
            set
            {
                mCircleView.Background.Alpha = value;
                mProgress.Alpha = value;
            }
        }


        /**
     * The refresh indicator starting and resting position is always positioned
     * near the top of the refreshing content. This position is a consistent
     * location, but can be adjusted in either direction based on whether or not
     * there is a toolbar or actionbar present.
     * <p>
     * <strong>Note:</strong> Calling this will reset the position of the refresh indicator to
     * <code>start</code>.
     * </p>
     *
     * @param scale Set to true if there is no view at a higher z-order than where the progress
     *              spinner is set to appear. Setting it to true will cause indicator to be scaled
     *              up rather than clipped.
     * @param start The offset in pixels from the top of this view at which the
     *              progress spinner should appear.
     * @param end The offset in pixels from the top of this view at which the
     *            progress spinner should come to rest after a successful swipe
     *            gesture.
     */
        public void SetHeaderProgressViewOffset(bool scale, int start, int end)
        {
            mHeaderScale = scale;
            mHeaderOriginalOffsetTop = start;
            mHeaderSpinnerOffsetEnd = end;
            mHeaderUsingCustomStart = true;
            ResetHeader();
            mHeaderRefreshing = false;
        }

        /**
         * @return The offset in pixels from the top of this view at which the progress spinner should
         *         appear.
         */
        public int GetHeaderProgressViewStartOffset()
        {
            return mHeaderOriginalOffsetTop;
        }

        /**
         * @return The offset in pixels from the top of this view at which the progress spinner should
         *         come to rest after a successful swipe gesture.
         */
        public int GetHeaderProgressViewEndOffset()
        {
            return mHeaderSpinnerOffsetEnd;
        }

        /**
         * The refresh indicator resting position is always positioned near the top
         * of the refreshing content. This position is a consistent location, but
         * can be adjusted in either direction based on whether or not there is a
         * toolbar or actionbar present.
         *
         * @param scale Set to true if there is no view at a higher z-order than where the progress
         *              spinner is set to appear. Setting it to true will cause indicator to be scaled
         *              up rather than clipped.
         * @param end The offset in pixels from the top of this view at which the
         *            progress spinner should come to rest after a successful swipe
         *            gesture.
         */
        public void SetHeaderProgressViewEndTarget(bool scale, int end)
        {
            mHeaderSpinnerOffsetEnd = end;
            mHeaderScale = scale;
            mCircleView.Invalidate();
        }

        /**
         * One of DEFAULT, or LARGE.
         */
        public void SetHeaderProgressCircleSize(int size)
        {
            if (size != MaterialProgressDrawable.LARGE && size != MaterialProgressDrawable.DEFAULT)
            {
                return;
            }
            DisplayMetrics metrics = Resources.DisplayMetrics;
            if (size == MaterialProgressDrawable.LARGE)
            {
                mCircleDiameter = (int)(CIRCLE_DIAMETER_LARGE * metrics.Density);
            }
            else
            {
                mCircleDiameter = (int)(CIRCLE_DIAMETER * metrics.Density);
            }
            // force the bounds of the progress circle inside the circle view to
            // update by setting it to null before updating its size and then
            // re-setting it
            mCircleView.SetImageDrawable(null);
            mProgress.UpdateSizes(size);
            mCircleView.SetImageDrawable(mProgress);
        }

        /**
         * Simple constructor to use when creating a SwipeRefreshLayout from code.
         *
         * @param context
         */
        public RefreshLayout(Context context) : this(context, null)
        {

        }

        /**
         * Constructor that is called when inflating SwipeRefreshLayout from XML.
         *
         * @param context
         * @param attrs
         */
        public RefreshLayout(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            mTouchSlop = ViewConfiguration.Get(context).ScaledTouchSlop;
            mAnimateFooterOffsetToStartPosition
            = new AnimationFooterStarttoFinishPosition(this);
            mMediumAnimationDuration = Resources.GetInteger(
                    Android.Resource.Integer.ConfigMediumAnimTime);
            mShrinkTrigger = new ShrinkTrigger(this);
            SetWillNotDraw(false);
            mDecelerateInterpolator = new DecelerateInterpolator(DECELERATE_INTERPOLATION_FACTOR);
            mAccelerateInterpolator = new AccelerateInterpolator(ACCELERATE_INTERPOLATION_FACTOR);
            mShrinkAnimationListener = new ShrinkAnimationListener(this);
            mHeaderRefreshListener = new HeaderRefreshListener(this);
            mAnimateToCorrectPosition = new AnimateToCorrectPosition(this);
            mAnimateToStartPosition = new AnimateToStartPosition(this);
            mAnimateFooterToStartPosition = new AnimateFooterToStartPosition(this);


            DisplayMetrics metrics = Resources.DisplayMetrics;
            mCircleDiameter = (int)(CIRCLE_DIAMETER * metrics.Density);

            CreateProgressView();
            ViewCompat.SetChildrenDrawingOrderEnabled(this, true);
            // the absolute offset has to take into account that the circle starts at an offset
            mHeaderSpinnerOffsetEnd = (int)(DEFAULT_CIRCLE_TARGET * metrics.Density);
            mHeaderTotalDragDistance = mHeaderSpinnerOffsetEnd;
            mNestedScrollingParentHelper = new NestedScrollingParentHelper(this);

            mNestedScrollingChildHelper = new NestedScrollingChildHelper(this);
            NestedScrollingEnabled = true;
            
            mHeaderOriginalOffsetTop = mHeaderCurrentTargetOffsetTop = -mCircleDiameter;
            MoveToStart(1.0f);
            mReturnToStartPosition = new ReturnToStartPosition(this);
            TypedArray a = context.ObtainStyledAttributes(attrs, LAYOUT_ATTRS);
            Enabled = (a.GetBoolean(0, true));
            a.Recycle();
            mReturnToStartPositionListener = new ReturnToStartPositionListener(this);
            mProgressBar = new SwipeProgressBar(this);
            mProgressBarHeight = (int)(metrics.Density * PROGRESS_BAR_HEIGHT);
            mCancel = new Cancel(this);
        }

        protected override int GetChildDrawingOrder(int childCount, int i)
        {
            if (mCircleViewIndex < 0)
            {
                return i;
            }
            else if (i == childCount - 1)
            {
                // Draw the selected child last
                return mCircleViewIndex;
            }
            else if (i >= mCircleViewIndex)
            {
                // Move the children after the selected child earlier one
                return i + 1;
            }
            else
            {
                // Keep the children before the selected child the same
                return i;
            }
        }

        private void CreateProgressView()
        {
            mCircleView = new CircleImageView(Context, CIRCLE_BG_LIGHT);
            mProgress = new MaterialProgressDrawable(Context, this);
            mProgress.SetBackgroundColor(CIRCLE_BG_LIGHT);
            mCircleView.SetImageDrawable(mProgress);
            mCircleView.Visibility = ViewStates.Gone;
            AddView(mCircleView);
        }


        /**
     * Set the listener to be notified when a refresh is triggered via the swipe
     * gesture.
     */
        public void SetOnRefreshListener(IOnRefreshListener listener)
        {
            mListener = listener;
        }

        /**
         * Pre API 11, alpha is used to make the progress circle appear instead of scale.
         */
        private bool IsAlphaUsedForScale()
        {
            return Build.VERSION.SdkInt < BuildVersionCodes.Honeycomb;

        }

        /**
         * Is user allowed to swipe header
         */
        public bool EnableSwipeHeader
        {
            get
            {
                return mEnableSwipeHeader;
            }
            set
            {
                mEnableSwipeHeader = value;
            }
        }


        public bool EnableSwipeFooter
        {
            get
            {
                return mEnableSwipeFooter;
            }
            set
            {
                mEnableSwipeFooter = value;
            }
        }

        /**
         * Notify the widget that refresh state has changed. Do not call this when
         * refresh is triggered by a swipe gesture.
         *
         * @param refreshing Whether or not the view should show refresh progress.
         */
        public bool HeaderRefreshing
        {
            set
            {
                if (mFooterRefreshing && value)
                {
                    // Can't header and footer refresh both
                    return;
                }

                if (value && mHeaderRefreshing != value)
                {
                    // scale and show
                    mHeaderRefreshing = value;
                    int endTarget = 0;
                    if (!mHeaderUsingCustomStart)
                    {
                        endTarget = mHeaderSpinnerOffsetEnd + mHeaderOriginalOffsetTop;
                    }
                    else
                    {
                        endTarget = mHeaderSpinnerOffsetEnd;
                    }
                    SetHeaderTargetOffsetTopAndBottom(endTarget - mHeaderCurrentTargetOffsetTop,
                            true /* requires update */);
                    mHeaderNotify = false;
                    StartScaleUpAnimation(mHeaderRefreshListener);
                }
                else
                {
                    SetHeaderRefreshing(value, false /* notify */);
                }
            }
        }

        /**
         * Notify the widget that refresh state has changed. Do not call this when
         * refresh is triggered by a swipe gesture.
         *
         * @param refreshing Whether or not the view should show refresh progress.
         */
        public bool FooterRefreshing
        {
            set
            {
                if (mHeaderRefreshing && value)
                {
                    // Can't header and footer refresh both
                    return;
                }

                if (mFooterRefreshing != value)
                {
                    EnsureTarget();
                    mFooterCurrPercentage = 0;
                    mFooterRefreshing = value;
                    if (mFooterRefreshing)
                    {
                        mProgressBar.Start();
                    }
                    else
                    {
                        mProgressBar.Stop();
                    }
                }
            }
        }


        private void StartScaleUpAnimation(Animation.IAnimationListener listener)
        {
            mCircleView.Visibility = ViewStates.Visible;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
            {
                // Pre API 11, alpha is used in place of scale up to show the
                // progress circle appearing.
                // Don't adjust the alpha during appearance otherwise.
                mProgress.Alpha =MAX_ALPHA;
            }
            mHeaderScaleAnimation = new HeaderScaleAnimation(this);
            mHeaderScaleAnimation.Duration = mMediumAnimationDuration;
            if (listener != null) {
                mCircleView.SetAnimationListener(listener);
            }
            mCircleView.ClearAnimation();
            mCircleView.StartAnimation(mHeaderScaleAnimation);
        }

        public class HeaderScaleAnimation : Animation {
            RefreshLayout layout;
            public HeaderScaleAnimation(RefreshLayout refresh)
            {
                layout = refresh;
            }
            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                layout.SetAnimationProgress(interpolatedTime);
            }
        }

        /**
         * Pre API 11, this does an alpha animation.
         * @param progress
         */
        void SetAnimationProgress(float progress)
        {
            if (IsAlphaUsedForScale())
            {
               HeaderColorViewAlpha = ((int)(progress * MAX_ALPHA));
            }
            else
            {
                ViewCompat.SetScaleX(mCircleView, progress);
                ViewCompat.SetScaleY(mCircleView, progress);
            }
        }

        public HeaderRefreshListener mHeaderRefreshListener;

        public class HeaderRefreshListener : Java.Lang.Object, Animation.IAnimationListener
        {
            RefreshLayout layout;
            public HeaderRefreshListener(RefreshLayout refresh)
            {
                layout = refresh;
            }

            public void OnAnimationEnd(Animation animation)
            {
                if (layout.mHeaderRefreshing)
                {
                    // Make sure the progress view is fully visible
                    layout.mProgress. Alpha =MAX_ALPHA;
                    layout.mProgress.Start();
                    if (layout.mHeaderNotify)
                    {
                        if (layout.mListener != null)
                        {
                            layout.mListener.OnHeaderRefresh();
                        }
                        layout.OnHeaderRefresh(this, EventArgs.Empty);
                    }
                    layout.mHeaderCurrentTargetOffsetTop = layout.mCircleView.Top;
                }
                else
                {
                    layout.ResetHeader();
                }
            }

            public void OnAnimationRepeat(Animation animation)
            {
                
            }

            public void OnAnimationStart(Animation animation)
            {
                
            }
        }

        private void SetHeaderRefreshing(bool refreshing, bool notify)
        {
            if (mFooterRefreshing && refreshing)
            {
                // Can't header and footer refresh both
                return;
            }

            if (mHeaderRefreshing != refreshing)
            {
                mHeaderNotify = notify;
                EnsureTarget();
                mHeaderRefreshing = refreshing;
                if (mHeaderRefreshing)
                {
                    AnimateHeaderOffsetToCorrectPosition(mHeaderCurrentTargetOffsetTop,
                        mHeaderRefreshListener);
                }
                else
                {
                    StartScaleDownAnimation(mHeaderRefreshListener);
                }
            }
        }

        public void StartScaleDownAnimation(Animation.IAnimationListener listener)
        {
            mHeaderScaleDownAnimation = new HeaderScaleDownAnimation(this);
            mHeaderScaleDownAnimation.Duration = SCALE_DOWN_DURATION;
            mCircleView.SetAnimationListener(listener);
            mCircleView.ClearAnimation();
            mCircleView.StartAnimation(mHeaderScaleDownAnimation);
        }

        public class HeaderScaleDownAnimation : Animation
        {
            RefreshLayout layout;
            public HeaderScaleDownAnimation(RefreshLayout refresh)
            {
                layout = refresh;
            }
            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                layout.SetAnimationProgress(1 - interpolatedTime);
            }
        }

        private void StartProgressAlphaStartAnimation()
        {
            mHeaderAlphaStartAnimation = StartAlphaAnimation(mProgress.Alpha, STARTING_PROGRESS_ALPHA);
        }


        private void StartProgressAlphaMaxAnimation()
        {
            mHeaderAlphaMaxAnimation = StartAlphaAnimation(mProgress.Alpha, MAX_ALPHA);
        }


        private Animation StartAlphaAnimation(int startingAlpha, int endingAlpha)
        {
            // Pre API 11, alpha is used in place of scale. Don't also use it to
            // show the trigger point.
            if (mHeaderScale && IsAlphaUsedForScale())
            {
                return null;
            }
            Animation alpha = new AlphaAnimation(this,startingAlpha,endingAlpha);
            alpha.Duration =ALPHA_ANIMATION_DURATION;
            // Clear out the previous animation listeners.
            mCircleView.SetAnimationListener(null);
            mCircleView.ClearAnimation();
            mCircleView.StartAnimation(alpha);
            return alpha;
        }

        public class AlphaAnimation : Animation
        {
            RefreshLayout layout;
            int startingAlpha,  endingAlpha;
            public AlphaAnimation(RefreshLayout refresh, int startingAlpha, int endingalpha)
            {
                this.startingAlpha = startingAlpha;
                endingAlpha = endingalpha;
                layout = refresh;
            }
            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                layout.mProgress.Alpha =(
                    (int)(startingAlpha + ((endingAlpha - startingAlpha) * interpolatedTime)));
            }
        }

        /**
         * Set the background color of the progress spinner disc.
         *
         * @param colorRes Resource id of the color.
         */
        public void SetHeaderProgressBackgroundColorSchemeResource(int colorRes)
        {
            SetHeaderProgressBackgroundColorSchemeColor(ContextCompat.GetColor(Context, colorRes));
        }

        /**
         * Set the background color of the progress spinner disc.
         *
         * @param color
         */
        public void SetHeaderProgressBackgroundColorSchemeColor(int color)
        {
            mCircleView.SetBackgroundColor( new  Color(color));
            mProgress.SetBackgroundColor(color);
        }

        /**
         * Set the color resources used in the progress animation from color resources.
         * The first color will also be the color of the bar that grows in response
         * to a user swipe gesture.
         *
         * @param colorResIds
         */
        public void SetHeaderColorSchemeResources(int[] colorResIds)
        {
            Context context = Context;
            int[] colorRes = new int[colorResIds.Length];
            for (int i = 0; i < colorResIds.Length; i++)
            {
                colorRes[i] = ContextCompat.GetColor(context, colorResIds[i]);
            }
            SetHeaderColorSchemeColors(colorRes);
        }

        /**
         * Set the colors used in the progress animation. The first
         * color will also be the color of the bar that grows in response to a user
         * swipe gesture.
         *
         * @param colors
         */
        public void SetHeaderColorSchemeColors(int[] colors)
        {
            EnsureTarget();
            mProgress.SetColorSchemeColors(colors);
        }

        /**
         * Set the colors used in the progress animation from color resources.
         * The first color will also be the color of the bar that grows in response
         * to a user swipe gesture.
         */
        public void SetFooterColorSchemeResources(int[] colorResIds)
        {
            Context context = Context;
            int[] colorRes = new int[colorResIds.Length];
            for (int i = 0; i < colorResIds.Length; i++)
            {
                colorRes[i] = ContextCompat.GetColor(context, colorResIds[i]);
            }
            SetFooterColorSchemeColors(colorRes);
        }

        /**
         * Set the colors used in the progress animation. The first color will
         * also be the color of the bar that grows in response to a user swipe
         * gesture.
         */
        public void SetFooterColorSchemeColors(int[] colors)
        {
            EnsureTarget();
            mProgressBar.SetColorScheme(colors);
        }

        /**
         * @return Whether the SwipeRefreshWidget is actively showing refresh
         *         progress.
         */
        public bool IsRefreshing()
        {
            return mHeaderRefreshing || mFooterRefreshing;
        }

        public bool IsHeaderRefreshing()
        {
            return mHeaderRefreshing;
        }

        public bool IsFooterRefreshing()
        {
            return mFooterRefreshing;
        }

        private void EnsureTarget()
        {
            // Don't bother getting the parent height if the parent hasn't been laid
            // out yet.
            if (mTarget == null)
            {
                for (int i = 0; i < ChildCount; i++)
                {
                    View child = GetChildAt(i);
                    if (!child.Equals(mCircleView))
                    {
                        mTarget = child;
                        break;
                    }
                }
            }
            if (mFooterDistanceToTriggerSync == -1)
            {
                if (Parent != null && ((View)Parent).Height > 0)
                {
                    DisplayMetrics metrics = Resources.DisplayMetrics;
                    mFooterDistanceToTriggerSync = (int)Math.Min(
                            ((View)Parent).Height * MAX_SWIPE_DISTANCE_FACTOR,
                            REFRESH_TRIGGER_DISTANCE * metrics.Density);
                }
            }
        }

        /**
         * Set the distance to trigger a sync in dips
         *
         * @param distance
         */
        public void SetHeaderDistanceToTriggerSync(int distance)
        {
            mHeaderTotalDragDistance = distance;
        }

        private void SetTriggerPercentage(float percent)
        {
            /*
            if (percent == 0f) {
                // No-op. A null trigger means it's uninitialized, and setting it to zero-percent
                // means we're trying to reset state, so there's nothing to reset in this case.
                mFooterCurrPercentage = 0;
                return;
            }
            */
            mFooterCurrPercentage = percent;
            mProgressBar.TriggerPercentage = percent;
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);
            mProgressBar.Draw(canvas);
        }


        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            int width = MeasuredWidth;
            int height = MeasuredHeight;
            if (ChildCount == 0)
            {
                return;
            }
            if (mTarget == null)
            {
                EnsureTarget();
            }
            if (mTarget == null)
            {
                return;
            }
            View child = mTarget;
            int childLeft = PaddingLeft;
            int childTop = PaddingTop;
            int childWidth = width - PaddingLeft - PaddingRight;
            int childHeight = height - PaddingTop - PaddingBottom;
            child.Layout(childLeft, childTop, childLeft + childWidth, childTop + childHeight);
            int circleWidth = mCircleView.MeasuredWidth;
            int circleHeight = mCircleView.MeasuredHeight;
            mCircleView.Layout((width / 2 - circleWidth / 2), mHeaderCurrentTargetOffsetTop,
                    (width / 2 + circleWidth / 2), mHeaderCurrentTargetOffsetTop + circleHeight);
            mProgressBar.SetBounds(0, height - mProgressBarHeight, width, height);
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);

            if (mTarget == null)
            {
                EnsureTarget();
            }
            if (mTarget == null)
            {
                return;
            }
            mTarget.Measure(MeasureSpec.MakeMeasureSpec(
                    MeasuredWidth - PaddingLeft - PaddingRight,
                    MeasureSpecMode.Exactly), MeasureSpec.MakeMeasureSpec(
                    MeasuredHeight - PaddingTop - PaddingBottom, MeasureSpecMode.Exactly));
            mCircleView.Measure(MeasureSpec.MakeMeasureSpec(mCircleDiameter, MeasureSpecMode.Exactly),
                    MeasureSpec.MakeMeasureSpec(mCircleDiameter, MeasureSpecMode.Exactly));
            mCircleViewIndex = -1;
            // Get the index of the circleview.
            for (int index = 0; index < ChildCount; index++)
            {
                if (GetChildAt(index) == mCircleView)
                {
                    mCircleViewIndex = index;
                    break;
                }
            }
        }

        /**
     * Get the diameter of the progress circle that is displayed as part of the
     * swipe to refresh layout.
     *
     * @return Diameter in pixels of the progress circle view.
     */
        public int HeaderProgressCircleDiameter
        {
            get
            {
                return mCircleDiameter;
            }
        }

        /**
         * @return Whether it is possible for the child view of this layout to
         *         scroll up. Override this if the child view is a custom view.
         */
        public bool CanChildScrollUp()
        {
            if (mChildScrollCallback != null)
            {
                return mChildScrollCallback.canChildScrollUp(this, mTarget);
            }
            if (Build.VERSION.SdkInt < BuildVersionCodes.IceCreamSandwich)
            {
                if (mTarget is AbsListView) {
                    AbsListView absListView = (AbsListView)mTarget;
                    return absListView.ChildCount > 0
                            && (absListView.FirstVisiblePosition > 0 || absListView.GetChildAt(0)
                                    .Top < absListView.PaddingTop);
                } else {
                    return ViewCompat.CanScrollVertically(mTarget, -1) || mTarget.ScrollY > 0;
                }
            }
            else
            {
                return ViewCompat.CanScrollVertically(mTarget, -1);
            }
        }

        /**
         * Set a callback to override {@link SwipeRefreshLayout#CanChildScrollUp()} method. Non-null
         * callback will return the value provided by the callback and ignore all internal logic.
         * @param callback Callback that should be called when CanChildScrollUp() is called.
         */
        public void SetOnChildScrollUpCallback(OnChildScrollCallback callback = null)
        {
            mChildScrollCallback = callback;
        }

        /**
         * @return Whether it is possible for the child view of this layout to
         *         scroll down. Override this if the child view is a custom view.
         */
        public bool CanChildScrollDown()
        {
            if (mChildScrollCallback != null)
            {
                return mChildScrollCallback.canChildScrollDown(this, mTarget);
            }
            if (mTarget is AbsListView) {
                AbsListView absListView = (AbsListView)mTarget;
                return absListView.ChildCount > 0
                        && (absListView.LastVisiblePosition < absListView.Count - 1 ||
                        absListView.GetChildAt(absListView.ChildCount - 1).Bottom <
                                absListView.Height - absListView.PaddingBottom);
            } else {
                return ViewCompat.CanScrollVertically(mTarget, 1);
            }
        }

        /**
         * @return {@code true} if child view almost scroll to bottom.
         */
        public bool IsAlmostBottom()
        {
            if (null == mTarget)
            {
                return false;
            }

            if (mTarget is AbsListView) {
                AbsListView absListView = (AbsListView)mTarget;
                return absListView.LastVisiblePosition >= absListView.Count - 1;
            } else if (mTarget is Android.Support.V4.View.IScrollingView) {
                IScrollingView scrollingView = (IScrollingView)mTarget;
                int offset = scrollingView.ComputeVerticalScrollOffset();
                int range = scrollingView.ComputeVerticalScrollRange() -
                        scrollingView.ComputeVerticalScrollExtent();
                return offset >= range;
            } else {
                return !ViewCompat.CanScrollVertically(mTarget, 1);
            }
        }

        public override bool OnInterceptTouchEvent(MotionEvent ev)
        {
            try
            {
                EnsureTarget();

                int action = MotionEventCompat.GetActionMasked(ev);

                if (mReturningToStart && ev.ActionMasked == MotionEventActions.Down)
                {
                    mReturningToStart = false;
                }

                bool mIsBeingDragged = false;

                if (Enabled && !mReturningToStart && !mHeaderRefreshing && !mFooterRefreshing)
                {
                    if (!mIsFooterBeingDragged && mEnableSwipeHeader && !CanChildScrollUp())
                    {
                        mIsBeingDragged = HeaderInterceptTouchEvent(ev);
                    }

                    if (!mIsHeaderBeingDragged && mEnableSwipeFooter && !CanChildScrollDown())
                    {
                        mIsBeingDragged |= FooterInterceptTouchEvent(ev);
                    }
                }

                return mIsBeingDragged;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private bool HeaderInterceptTouchEvent(MotionEvent ev)
        {
            int pointerIndex;
            int action = MotionEventCompat.GetActionMasked(ev);
            switch (ev.ActionMasked)
            {
                case MotionEventActions.Down:
                    SetHeaderTargetOffsetTopAndBottom(mHeaderOriginalOffsetTop - mCircleView.Top, true);
                    mActivePointerId = ev.GetPointerId(0);
                    mIsHeaderBeingDragged = false;

                    pointerIndex = ev.FindPointerIndex(mActivePointerId);
                    if (pointerIndex < 0)
                    {
                        return false;
                    }
                    mInitialDownY = ev.GetY(pointerIndex);
                    break;

                case MotionEventActions.Move:
                    if (mActivePointerId == INVALID_POINTER)
                    {
                        //Log.e(LOG_TAG, "Got ACTION_MOVE event but don't have an active pointer id.");
                        return false;
                    }

                    pointerIndex = ev.FindPointerIndex(mActivePointerId);
                    if (pointerIndex < 0)
                    {
                        return false;
                    }
                    float y = ev.GetY(pointerIndex);
                    StartHeaderDragging(y);
                    break;

                case MotionEventActions.PointerUp:
                    OnSecondaryPointerUp(ev);
                    break;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    mIsHeaderBeingDragged = false;
                    mActivePointerId = INVALID_POINTER;
                    break;
            }

            return mIsHeaderBeingDragged;
        }

        private bool FooterInterceptTouchEvent(MotionEvent ev)
        {
            int pointerIndex;
            int action = MotionEventCompat.GetActionMasked(ev);
            switch (ev.ActionMasked)
            {
                case MotionEventActions.Down:
                    mActivePointerId = ev.GetPointerId(0);
                    mIsFooterBeingDragged = false;
                    mFooterCurrPercentage = 0;

                    pointerIndex = ev.FindPointerIndex(mActivePointerId);
                    if (pointerIndex < 0)
                    {
                        return false;
                    }
                    mInitialDownY = ev.GetY(pointerIndex);
                    break;

                case MotionEventActions.Move:
                    if (mActivePointerId == INVALID_POINTER)
                    {
                        //Log.e(LOG_TAG, "Got ACTION_MOVE event but don't have an active pointer id.");
                        return false;
                    }

                    pointerIndex = ev.FindPointerIndex(mActivePointerId);
                    if (pointerIndex < 0)
                    {
                        //Log.e(LOG_TAG, "Got ACTION_MOVE event but have an invalid active pointer id.");
                        return false;
                    }

                    float y = ev.GetY(pointerIndex);
                    float yDiff = y - mInitialDownY;
                    if (yDiff < -mTouchSlop)
                    {
                        mIsFooterBeingDragged = true;
                        mInitialMotionY = mInitialDownY - mTouchSlop;
                    }
                    break;

                //todo motioneventcompat to motioneventaction
                case MotionEventActions.PointerUp:
                    OnSecondaryPointerUp(ev);
                    break;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    mIsFooterBeingDragged = false;
                    mFooterCurrPercentage = 0;
                    mActivePointerId = INVALID_POINTER;
                    break;
            }

            return mIsFooterBeingDragged;
        }

        public override void RequestDisallowInterceptTouchEvent(bool disallowIntercept)
        {
            // if this is a List < L or another view that doesn't support nested
            // scrolling, ignore this request so that the vertical scroll event
            // isn't stolen
            if ((Build.VERSION.SdkInt < BuildVersionCodes.Lollipop && mTarget is AbsListView)
                || (mTarget != null && !ViewCompat.IsNestedScrollingEnabled(mTarget))) {
                // Nope.
            } else {
                base.RequestDisallowInterceptTouchEvent(disallowIntercept);
            }
        }

        public override bool OnStartNestedScroll(View child, View target, [GeneratedEnum] ScrollAxis nestedScrollAxes)
        {
            return Enabled && !mReturningToStart && !mHeaderRefreshing
                && ((int)nestedScrollAxes & ViewCompat.ScrollAxisVertical) != 0;
        }

        public override void OnNestedScrollAccepted(View child, View target, [GeneratedEnum] ScrollAxis axes)
        {
            // Reset the counter of how much leftover scroll needs to be consumed.
            mNestedScrollingParentHelper.OnNestedScrollAccepted(child, target, (int)axes);
            // Dispatch up to the nested parent
            StartNestedScroll(axes & (ScrollAxis)ViewCompat.ScrollAxisVertical);
            mTotalUnconsumed = 0;
            mNestedScrollInProgress = true;
        }

        public override void OnNestedPreScroll(View target, int dx, int dy, int[] consumed)
        {
            // If we are in the middle of consuming, a scroll, then we want to move the spinner back up
            // before allowing the list to scroll
            if (dy > 0 && mTotalUnconsumed > 0)
            {
                if (dy > mTotalUnconsumed)
                {
                    consumed[1] = dy - (int)mTotalUnconsumed;
                    mTotalUnconsumed = 0;
                }
                else
                {
                    mTotalUnconsumed -= dy;
                    consumed[1] = dy;
                }
                MoveSpinner(mTotalUnconsumed);
            }

            // If a client layout is using a custom start position for the circle
            // view, they mean to hide it again before scrolling the child view
            // If we get back to mTotalUnconsumed == 0 and there is more to go, hide
            // the circle so it isn't exposed if its blocking content is moved
            if (mHeaderUsingCustomStart && dy > 0 && mTotalUnconsumed == 0
                    && Math.Abs(dy - consumed[1]) > 0)
            {
                mCircleView.Visibility = ViewStates.Gone;
            }

            // Now let our nested parent consume the leftovers
            int[] parentConsumed = mParentScrollConsumed;
            if (DispatchNestedPreScroll(dx - consumed[0], dy - consumed[1], parentConsumed, null))
            {
                consumed[0] += parentConsumed[0];
                consumed[1] += parentConsumed[1];
            }
        }

        public override ScrollAxis NestedScrollAxes
        {
            get
            {
                return (ScrollAxis)mNestedScrollingParentHelper.NestedScrollAxes;
            }
        }

        public override void OnStopNestedScroll(View child)
        {
            mNestedScrollingParentHelper.OnStopNestedScroll(child);
            mNestedScrollInProgress = false;
            // Finish the spinner for nested scrolling if we ever consumed any
            // unconsumed nested scroll
            if (mTotalUnconsumed > 0)
            {
                FinishSpinner(mTotalUnconsumed);
                mTotalUnconsumed = 0;
            }
            // Dispatch up our nested parent
            StopNestedScroll();
        }

        public override void OnNestedScroll(View target, int dxConsumed, int dyConsumed, int dxUnconsumed, int dyUnconsumed)
        {
            // Dispatch up to the nested parent first
            DispatchNestedScroll(dxConsumed, dyConsumed, dxUnconsumed, dyUnconsumed,
                    mParentOffsetInWindow);

            // This is a bit of a hack. Nested scrolling works from the bottom up, and as we are
            // sometimes between two nested scrolling views, we need a way to be able to know when any
            // nested scrolling parent has stopped handling events. We do that by using the
            // 'offset in window 'functionality to see if we have been moved from the event.
            // This is a decent indication of whether we should take over the event stream or not.
            int dy = dyUnconsumed + mParentOffsetInWindow[1];
            if (dy < 0 && !CanChildScrollUp())
            {
                mTotalUnconsumed += Math.Abs(dy);
                MoveSpinner(mTotalUnconsumed);
            }
        }

        public override bool NestedScrollingEnabled
        {
            get
            {
                return mNestedScrollingChildHelper.NestedScrollingEnabled;
            }

            set
            {
                mNestedScrollingChildHelper.NestedScrollingEnabled = value;
            }
        }

        public override bool StartNestedScroll([GeneratedEnum] ScrollAxis axes)
        {
            return mNestedScrollingChildHelper.StartNestedScroll((int)axes);
        }

        public override void StopNestedScroll()
        {
            mNestedScrollingChildHelper.StopNestedScroll();
        }

        public override bool HasNestedScrollingParent
        {
            get
            {
                return mNestedScrollingChildHelper.HasNestedScrollingParent;
            }
        }

        public override bool DispatchNestedScroll(int dxConsumed, int dyConsumed, int dxUnconsumed, int dyUnconsumed, int[] offsetInWindow)
        {
            return mNestedScrollingChildHelper.DispatchNestedScroll(dxConsumed, dyConsumed,
                dxUnconsumed, dyUnconsumed, offsetInWindow);
        }

        public override bool DispatchNestedPreScroll(int dx, int dy, int[] consumed, int[] offsetInWindow)
        {
            return mNestedScrollingChildHelper.DispatchNestedPreScroll(
                            dx, dy, consumed, offsetInWindow);
        }

        public override bool OnNestedFling(View target, float velocityX, float velocityY, bool consumed)
        {
            return DispatchNestedFling(velocityX, velocityY, consumed);
        }

        public override bool OnNestedPreFling(View target, float velocityX, float velocityY)
        {
            return DispatchNestedPreFling(velocityX, velocityY);
        }

        public override bool DispatchNestedFling(float velocityX, float velocityY, bool consumed)
        {
            return mNestedScrollingChildHelper.DispatchNestedFling(velocityX, velocityY, consumed);
        }

        public override bool DispatchNestedPreFling(float velocityX, float velocityY)
        {
            return mNestedScrollingChildHelper.DispatchNestedPreFling(velocityX, velocityY);
        }

        private bool IsAnimationRunning(Animation animation)
        {
            return animation != null && animation.HasStarted && !animation.HasEnded;
        }

        private void MoveSpinner(float overscrollTop)
        {
            mProgress.showArrow(true);
            float originalDragPercent = overscrollTop / mHeaderTotalDragDistance;

            float dragPercent = Math.Min(1f, Math.Abs(originalDragPercent));
            float adjustedPercent = (float)Math.Max(dragPercent - .4, 0) * 5 / 3;
            float extraOS = Math.Abs(overscrollTop) - mHeaderTotalDragDistance;
            float slingshotDist = mHeaderUsingCustomStart ? mHeaderSpinnerOffsetEnd - mHeaderOriginalOffsetTop
                    : mHeaderSpinnerOffsetEnd;
            float tensionSlingshotPercent = Math.Max(0, Math.Min(extraOS, slingshotDist * 2)
                    / slingshotDist);
            float tensionPercent = (float)((tensionSlingshotPercent / 4) - Math.Pow(
                    (tensionSlingshotPercent / 4), 2)) * 2f;
            float extraMove = (slingshotDist) * tensionPercent * 2;

            int targetY = mHeaderOriginalOffsetTop + (int)((slingshotDist * dragPercent) + extraMove);
            // where 1.0f is a full circle
            if (mCircleView.Visibility != ViewStates.Visible)
            {
                mCircleView.Visibility = ViewStates.Visible;
            }
            if (!mHeaderScale)
            {
                ViewCompat.SetScaleX(mCircleView, 1f);
                ViewCompat.SetScaleY(mCircleView, 1f);
            }

            if (mHeaderScale)
            {
                SetAnimationProgress(Math.Min(1f, overscrollTop / mHeaderTotalDragDistance));
            }
            if (overscrollTop < mHeaderTotalDragDistance)
            {
                if (mProgress.Alpha > STARTING_PROGRESS_ALPHA
                        && !IsAnimationRunning(mHeaderAlphaStartAnimation))
                {
                    // Animate the alpha
                    StartProgressAlphaStartAnimation();
                }
            }
            else
            {
                if (mProgress.Alpha < MAX_ALPHA && !IsAnimationRunning(mHeaderAlphaMaxAnimation))
                {
                    // Animate the alpha
                    StartProgressAlphaMaxAnimation();
                }
            }
            float strokeStart = adjustedPercent * .8f;
            mProgress.setStartEndTrim(0f, Math.Min(MAX_PROGRESS_ANGLE, strokeStart));
            mProgress.setArrowScale(Math.Min(1f, adjustedPercent));

            float rotation = (-0.25f + .4f * adjustedPercent + tensionPercent * 2) * .5f;
            mProgress.setProgressRotation(rotation);
            SetHeaderTargetOffsetTopAndBottom(targetY - mHeaderCurrentTargetOffsetTop, true /* requires update */);
        }

        private void FinishSpinner(float overscrollTop)
        {
            if (overscrollTop > mHeaderTotalDragDistance)
            {
                SetHeaderRefreshing(true, true /* notify */);
            }
            else
            {
                // cancel refresh
                mHeaderRefreshing = false;
                mProgress.setStartEndTrim(0f, 0f);
                Animation.IAnimationListener listener = null;
                if (!mHeaderScale)
                {
                    listener = new HeaderAnimationListener(this);


                }
                AnimateHeaderOffsetToStartPosition(mHeaderCurrentTargetOffsetTop, listener);
                mProgress.showArrow(false);
            }
        }

        public class HeaderAnimationListener : Java.Lang.Object, Animation.IAnimationListener
        {
            RefreshLayout layout;
            public HeaderAnimationListener(RefreshLayout refresh)
            {
                layout = refresh;
            }

            public void OnAnimationEnd(Animation animation)
            {
                if (!layout.mHeaderScale)
                {
                    layout.StartScaleDownAnimation(null);
                }
            }

            public void OnAnimationRepeat(Animation animation)
            {
            }

            public void OnAnimationStart(Animation animation)
            {
            }
        }

        public override bool OnTouchEvent(MotionEvent e = null)
        {
            try
            {
                int action = MotionEventCompat.GetActionMasked(e);

                if (mReturningToStart && e.ActionMasked == MotionEventActions.Down)
                {
                    mReturningToStart = false;
                }

                if (Enabled && !mReturningToStart && !mHeaderRefreshing && !mFooterRefreshing)
                {
                    if (!mIsFooterBeingDragged && mEnableSwipeHeader && !CanChildScrollUp())
                    {
                        HeaderTouchEvent(e);
                    }

                    if (!mIsHeaderBeingDragged && mEnableSwipeFooter && !CanChildScrollDown())
                    {
                        FooterTouchEvent(e);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private bool HeaderTouchEvent(MotionEvent ev)
        {
            int pointerIndex;
            int action = MotionEventCompat.GetActionMasked(ev);
            switch (ev.ActionMasked)
            {
                case MotionEventActions.Down:
                    mActivePointerId = ev.GetPointerId(0);
                    mIsHeaderBeingDragged = false;
                    break;

                case MotionEventActions.Move:
                    {
                        pointerIndex = ev.FindPointerIndex(mActivePointerId);
                        if (pointerIndex < 0)
                        {
                            // Log.e(LOG_TAG, "Got ACTION_MOVE event but have an invalid active pointer id.");
                            return false;
                        }

                        float y = ev.GetY(pointerIndex);
                        StartHeaderDragging(y);

                        if (mIsHeaderBeingDragged)
                        {
                            float overscrollTop = (y - mInitialMotionY) * DRAG_RATE;
                            if (overscrollTop > 0)
                            {
                                MoveSpinner(overscrollTop);
                            }
                            else
                            {
                                return false;
                            }
                        }
                        break;
                    }
                case (MotionEventActions)MotionEventCompat.ActionPointerDown:
                    {
                        pointerIndex = MotionEventCompat.GetActionIndex(ev);
                        if (pointerIndex < 0)
                        {
                            //Log.e(LOG_TAG,
                            //      "Got ACTION_POINTER_DOWN event but have an invalid action index.");
                            return false;
                        }
                        mActivePointerId = ev.GetPointerId(pointerIndex);
                        break;
                    }

                case (MotionEventActions)MotionEventCompat.ActionPointerUp:
                    OnSecondaryPointerUp(ev);
                    break;

                case MotionEventActions.Up:
                    {
                        pointerIndex = ev.FindPointerIndex(mActivePointerId);
                        if (pointerIndex < 0)
                        {
                            //Log.e(LOG_TAG, "Got ACTION_UP event but don't have an active pointer id.");
                            return false;
                        }

                        if (mIsHeaderBeingDragged)
                        {
                            float y = ev.GetY(pointerIndex);
                            float overscrollTop = (y - mInitialMotionY) * DRAG_RATE;
                            mIsHeaderBeingDragged = false;
                            FinishSpinner(overscrollTop);
                        }
                        mActivePointerId = INVALID_POINTER;
                        return false;
                    }
                case MotionEventActions.Cancel:
                    pointerIndex = ev.FindPointerIndex(mActivePointerId);
                    if (pointerIndex < 0)
                    {
                        //Log.e(LOG_TAG, "Got ACTION_UP event but don't have an active pointer id.");
                        return false;
                    }

                    if (mIsHeaderBeingDragged)
                    {
                        mIsHeaderBeingDragged = false;
                        FinishSpinner(0);
                    }
                    mActivePointerId = INVALID_POINTER;
                    return false;
            }

            return true;
        }

        private void StartHeaderDragging(float y)
        {
            float yDiff = y - mInitialDownY;
            if (yDiff > mTouchSlop && !mIsHeaderBeingDragged)
            {
                mInitialMotionY = mInitialDownY + mTouchSlop;
                mIsHeaderBeingDragged = true;
                mProgress.Alpha =(STARTING_PROGRESS_ALPHA);
            }
        }

        private void StartFooterRefresh()
        {
            RemoveCallbacks(mCancel);
            mReturnToStartPosition.Run();
            FooterRefreshing=true;
            if(mListener!=null)
            mListener.OnFooterRefresh();
            OnFooterRefresh(this, EventArgs.Empty);
        }

        private bool FooterTouchEvent(MotionEvent ev)
        {
            int action = MotionEventCompat.GetActionMasked(ev);

            int pointerIndex;
            float y;
            float yDiff;
            switch (ev.ActionMasked)
            {
                case MotionEventActions.Down:
                    mActivePointerId = ev.GetPointerId(0);
                    mIsFooterBeingDragged = false;
                    mFooterCurrPercentage = 0;
                    break;

                case MotionEventActions.Move:
                    pointerIndex = ev.FindPointerIndex(mActivePointerId);
                    if (pointerIndex < 0)
                    {
                        //Log.e(LOG_TAG, "Got ACTION_MOVE event but have an invalid active pointer id.");
                        return false;
                    }

                    y = ev.GetY(pointerIndex);
                    if (!mIsFooterBeingDragged)
                    {
                        yDiff = y - mInitialDownY;
                        if (yDiff < -mTouchSlop)
                        {
                            mIsFooterBeingDragged = true;
                            mInitialMotionY = mInitialDownY - mTouchSlop;
                        }
                    }

                    if (mIsFooterBeingDragged)
                    {
                        yDiff = y - mInitialMotionY;
                        SetTriggerPercentage(
                                mAccelerateInterpolator.GetInterpolation(
                                        MathUtil.Clamp(-yDiff, 0, mFooterDistanceToTriggerSync) / mFooterDistanceToTriggerSync));
                    }
                    break;

                case (MotionEventActions)MotionEventCompat.ActionPointerDown:
                    {
                        int index = MotionEventCompat.GetActionIndex(ev);
                        mActivePointerId = ev.GetPointerId(index);
                        break;
                    }

                case (MotionEventActions)MotionEventCompat.ActionPointerUp:
                    OnSecondaryPointerUp(ev);
                    break;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    pointerIndex = ev.FindPointerIndex(mActivePointerId);
                    if (mActivePointerId == INVALID_POINTER && pointerIndex < 0)
                    {
                        if (ev.Action == MotionEventActions.Up)
                        {
                            //Log.e(LOG_TAG, "Got ACTION_UP event but don't have an active pointer id.");
                        }
                        return false;
                    }

                    try
                    {
                        y = ev.GetY(pointerIndex);
                    }
                    catch (Exception e)
                    {
                        y = 0;
                    }

                    yDiff = y - mInitialMotionY;

                    if (ev.Action == MotionEventActions.Up && -yDiff > mFooterDistanceToTriggerSync)
                    {
                        // User movement passed distance; trigger a refresh
                        StartFooterRefresh();
                    }
                    else
                    {
                        mCancel.Run();
                    }

                    mIsFooterBeingDragged = false;
                    mFooterCurrPercentage = 0;
                    mActivePointerId = INVALID_POINTER;
                    return false;
            }

            return mIsFooterBeingDragged;
        }

        private void AnimateHeaderOffsetToCorrectPosition(int from, Animation.IAnimationListener listener)
        {
            mHeaderFrom = from;
            mAnimateToCorrectPosition.Reset();
            mAnimateToCorrectPosition.Duration =ANIMATE_TO_TRIGGER_DURATION;
            mAnimateToCorrectPosition.Interpolator = mDecelerateInterpolator;
            if (listener != null)
            {
                mCircleView.SetAnimationListener(listener);
            }
            mCircleView.ClearAnimation();
            mCircleView.StartAnimation(mAnimateToCorrectPosition);
        }

        private void AnimateHeaderOffsetToStartPosition(int from, Animation.IAnimationListener listener)
        {
            if (mHeaderScale)
            {
                // Scale the item back down
                StartScaleDownReturnToStartAnimation(from, listener);
            }
            else
            {
                mHeaderFrom = from;
                mAnimateToStartPosition.Reset();
                mAnimateToStartPosition.Duration =ANIMATE_TO_START_DURATION;
                mAnimateToStartPosition.Interpolator= mDecelerateInterpolator;
                if (listener != null)
                {
                    mCircleView.SetAnimationListener(listener);
                }
                mCircleView.ClearAnimation();
                mCircleView.StartAnimation(mAnimateToStartPosition);
            }
        }

        AnimateToCorrectPosition mAnimateToCorrectPosition;
        public class AnimateToCorrectPosition : Animation
        {
            RefreshLayout layout;
            public AnimateToCorrectPosition(RefreshLayout refresh)
            {
                layout = refresh;
            }

            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                int targetTop = 0;
                int endTarget = 0;
                if (!layout.mHeaderUsingCustomStart)
                {
                    endTarget = layout.mHeaderSpinnerOffsetEnd - Math.Abs(layout.mHeaderOriginalOffsetTop);
                }
                else
                {
                    endTarget = layout.mHeaderSpinnerOffsetEnd;
                }
                targetTop = (layout.mHeaderFrom + (int)((endTarget - layout.mHeaderFrom) * interpolatedTime));
                int offset = targetTop - layout.mCircleView.Top;
                layout.SetHeaderTargetOffsetTopAndBottom(offset, false /* requires update */);
                layout.mProgress.setArrowScale(1 - interpolatedTime);
            }
        }

        void MoveToStart(float interpolatedTime)
        {
            int targetTop = 0;
            targetTop = (mHeaderFrom + (int)((mHeaderOriginalOffsetTop - mHeaderFrom) * interpolatedTime));
            int offset = targetTop - mCircleView.Top;
            SetHeaderTargetOffsetTopAndBottom(offset, false /* requires update */);
        }

        AnimateToStartPosition mAnimateToStartPosition;
        public class AnimateToStartPosition : Animation
        {
            RefreshLayout layout;
            public AnimateToStartPosition(RefreshLayout refresh)
            {
                layout = refresh;
            }

            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                layout.MoveToStart(interpolatedTime);
            }
        }


        private void StartScaleDownReturnToStartAnimation(int from,
                Animation.IAnimationListener listener)
        {
            mHeaderFrom = from;
            if (IsAlphaUsedForScale())
            {
                mHeaderStartingScale = mProgress.Alpha;
            }
            else
            {
                mHeaderStartingScale = ViewCompat.GetScaleX(mCircleView);
            }
            mHeaderScaleDownToStartAnimation = new HeaderScaleDownToStartAnimation(this);
            mHeaderScaleDownToStartAnimation.Duration = SCALE_DOWN_DURATION;
            if (listener != null) {
                mCircleView.SetAnimationListener(listener);
            }
            mCircleView.ClearAnimation();
            mCircleView.StartAnimation(mHeaderScaleDownToStartAnimation);
        }

        public class HeaderScaleDownToStartAnimation : Animation
        {
            RefreshLayout layout;
            public HeaderScaleDownToStartAnimation(RefreshLayout refresh)
            {
                layout = refresh;
            }

            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                float targetScale = (layout.mHeaderStartingScale + (-layout.mHeaderStartingScale * interpolatedTime));
                layout.SetAnimationProgress(targetScale);
                layout.MoveToStart(interpolatedTime);
            }

        }


        void SetHeaderTargetOffsetTopAndBottom(int offset, bool requiresUpdate)
        {
            mCircleView.BringToFront();
            ViewCompat.OffsetTopAndBottom(mCircleView, offset);
            mHeaderCurrentTargetOffsetTop = mCircleView.Top;
            if (requiresUpdate && Build.VERSION.SdkInt < BuildVersionCodes.Honeycomb)
            {
                Invalidate();
            }
        }

        private void AnimateFooterOffsetToStartPosition(int from, Animation.IAnimationListener listener)
        {
            mFooterFrom = from;
            mAnimateFooterToStartPosition.Reset();
            mAnimateFooterToStartPosition.Duration = mMediumAnimationDuration;
            mAnimateFooterToStartPosition.SetAnimationListener(listener);
            mAnimateFooterToStartPosition.Interpolator =mDecelerateInterpolator;
            mTarget.StartAnimation(mAnimateFooterToStartPosition);
        }

        private void UpdatePositionTimeout()
        {
            RemoveCallbacks(mCancel);
            PostDelayed(mCancel, RETURN_TO_ORIGINAL_POSITION_TIMEOUT);
        }

        private void OnSecondaryPointerUp(MotionEvent ev)
        {
            int pointerIndex = MotionEventCompat.GetActionIndex(ev);
            int pointerId = ev.GetPointerId(pointerIndex);
            if (pointerId == mActivePointerId)
            {
                // This was our active pointer going up. Choose a new
                // active pointer and adjust accordingly.
                int newPointerIndex = pointerIndex == 0 ? 1 : 0;
                mActivePointerId = ev.GetPointerId(newPointerIndex);
            }
        }

        /**
         * Classes that wish to be notified when the swipe gesture correctly
         * triggers a refresh should implement this interface.
         */
        public interface IOnRefreshListener
        {
            void OnHeaderRefresh();

            void OnFooterRefresh();
        }

        /**
         * Classes that wish to override {@link RefreshLayout#CanChildScrollUp()} method
         * and {@link RefreshLayout#CanChildScrollDown()} method behavior should implement this interface.
         */
        public interface OnChildScrollCallback
        {
            /**
             * Callback that will be called when {@link RefreshLayout#CanChildScrollUp()} method
             * is called to allow the implementer to override its behavior.
             *
             * @param parent SwipeRefreshLayout that this callback is overriding.
             * @param child The child view of SwipeRefreshLayout.
             *
             * @return Whether it is possible for the child view of parent layout to scroll up.
             */
            bool canChildScrollUp(RefreshLayout parent, View child = null);

            /**
             * Callback that will be called when {@link RefreshLayout#CanChildScrollDown()} method
             * is called to allow the implementer to override its behavior.
             *
             * @param parent SwipeRefreshLayout that this callback is overriding.
             * @param child The child view of SwipeRefreshLayout.
             *
             * @return Whether it is possible for the child view of parent layout to scroll down.
             */
            bool canChildScrollDown(RefreshLayout parent, View child = null);
        }

        /**
         * Simple AnimationListener to avoid having to implement unneeded methods in
         * AnimationListeners.
         */
        public class BaseAnimationListener : Java.Lang.Object, Animation.IAnimationListener
        {
            public virtual void OnAnimationEnd(Animation animation)
            {
                
            }

            public void OnAnimationRepeat(Animation animation)
            {
               
            }

            public void OnAnimationStart(Animation animation)
            {
               
            }
        }

        public AnimateFooterToStartPosition mAnimateFooterToStartPosition;

        public class AnimateFooterToStartPosition : Animation
        {
            RefreshLayout layout;
            public AnimateFooterToStartPosition(RefreshLayout refresh)
            {
                layout = refresh;
            }

            protected override void ApplyTransformation(float interpolatedTime, Transformation t)
            {
                int targetTop = 0;
                if (layout.mFooterFrom != layout.mFooterOriginalOffsetTop)
                {
                    targetTop = (layout.mFooterFrom + (int)((layout.mFooterOriginalOffsetTop - layout.mFooterFrom) * interpolatedTime));
                }
                int offset = targetTop - layout.mTarget.Top;
                int currentTop = layout.mTarget.Top;
                if (offset + currentTop > 0)
                {
                    offset = 0 - currentTop;
                }
            }
        }



    }
}