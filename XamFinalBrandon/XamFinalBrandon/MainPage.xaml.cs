using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Plugin.Media;
using Plugin.Media.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace XamFinalBrandon
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]

    public partial class MainPage : ContentPage
    {

        #region Fields
        //API keys for cognitive services
        private const string APIKEY = "6f820f50156d4a3bb202d4a5fd4ec5ec";
        private const string ENDPOINT = "https://imagereader555.cognitiveservices.azure.com/";
        //A CancellationToken object, which indicates whether cancellation is requested.
        CancellationTokenSource cts;
        #endregion

        #region Constructors
        public MainPage()
        {
            InitializeComponent();
        }
        #endregion

        #region UI Control Event Methods 
        private async void WebImageButton_Clicked(object sender, EventArgs e)
        {
            //cancel speech of previous image
            CancelSpeech();

            //url to random image
            Uri webImageUri = new Uri("https://placeimg.com/640/480");

            try
            {
                //Provides a base class for sending HTTP requests and receiving HTTP responses from a resource identified by a URI.
                using (HttpClient client = new HttpClient())
                {
                    //activity animation is displaying
                    theActivityIndicator.IsRunning = true;

                    
                    using (var response = await client.GetStreamAsync(webImageUri))
                    {
                        //create a  new instance of the MemoryStream class 
                        MemoryStream memoryStream = new MemoryStream();
                        await response.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        try
                        {
                            //get the image description from the memory stream
                            var result = await GetImageDescription(memoryStream);

                            ProcessImageResults(result);

                            //set the xaml element to the image from the memory stream
                            theImage.Source = ImageSource.FromStream(() => memoryStream);
                        }
                        catch (Exception ex)
                        {   
                            //if theres a an eror with thecomputer api vision display the error if not the image didn't load properly so display and error message.
                            if (ex is ComputerVisionErrorException)
                            {
                                theResults.Text = ex.Message;
                            }
                            else
                            {
                                { theResults.Text = "Failed to load imagine: " + ex.Message; }
                            }
                        }
                    }
                    //turn off the loading animation after the image is loaded
                    theActivityIndicator.IsRunning = false;
                }
            }
            catch (Exception ex)
            {
                theResults.Text = "Failed to load an image: " + ex.Message;
            }
        }
        
        //Allows access to local images to be used as an image source
        private async void LocalImageButton_Clicked(object sender, EventArgs e)
        {
            CancelSpeech();

            await CrossMedia.Current.Initialize();

            if (CrossMedia.Current.IsPickPhotoSupported)
            {
                var file = await CrossMedia.Current.PickPhotoAsync();

                //Return if the file is null - if the user cancels before picking an image return
                if (file == null)
                {
                    return;
                }

                theImage.Source = ImageSource.FromStream(() =>
                {
                    return file.GetStream();
                });

                GetImageDataFromFile(file);
            }
        }

        //Camera access to be used as an image source
        private async void CameraButton_Clicked(object sender, EventArgs e)
        {
            CancelSpeech();

            //open camera if available
            await CrossMedia.Current.Initialize();
            if (!CrossMedia.Current.IsCameraAvailable || !CrossMedia.Current.IsTakePhotoSupported)
            {
                await DisplayAlert("No Camera ", ":( No Camera Available", "Okay");
                return;
            }
            //take the photo and options for storing the file
            var file = await CrossMedia.Current.TakePhotoAsync(
                new StoreCameraMediaOptions
                {
                    Directory = "CogSample",
                    Name = "cogImage.jpg"
                });

            //if the user cancels return to avoid bugs
            if (file == null)
            {
                return;
            }
            //set the image xaml element to the image from the stream
            theImage.Source = ImageSource.FromStream(() =>
            {
                return file.GetStream();
            });

            GetImageDataFromFile(file);
        }
        #endregion

        #region Helper Methods 
        private async Task<ImageAnalysis> GetImageDescription(Stream imageStream)
        {
            ComputerVisionClient visionClient = new ComputerVisionClient(
                new ApiKeyServiceClientCredentials(APIKEY),
                new DelegatingHandler[] { });

            visionClient.Endpoint = ENDPOINT;

            // Specify the Image features to return
            List<VisualFeatureTypes> features = new List<VisualFeatureTypes>()
        {
            VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
            VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
            VisualFeatureTypes.Tags
        };

            // Create and use a duplicate MemoryStream

            MemoryStream memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            imageStream.Position = 0;

            return await visionClient.AnalyzeImageInStreamAsync(memoryStream, features, null);
        }

        private void ProcessImageResults(ImageAnalysis result)
        {
            //clears previous results text
            theResults.Text = string.Empty;


            if (result.Description.Captions.Count != 0)
            {
                //set the text to the results desciption
                theResults.Text = result.Description.Captions[0].Text;

                //create a new variable tthat containes the the description confidence
                var confidence = result.Description.Captions[0].Confidence;
                
                //formats the text and add % symbol
                theResults.Text += "\nConfidence (" + string.Format("{0:p0}", confidence) + ")\n";

                if (result.Tags.Count != 0)
                {
                    //iterate through the results array
                    for (int i = 0; i < result.Tags.Count; i++)
                    {
                        if (i < result.Tags.Count - 1)
                        {
                            theResults.Text += result.Tags[i].Name + ", ";
                        }
                        else if (i == result.Tags.Count - 1)
                        {
                            theResults.Text += result.Tags[i].Name;
                        }
                    }
                }
            }
            else
            {
                //display message if nothing was reconized by the api
                theResults.Text = "Nothing was recognized";
            }

            //calls speak method to read the text
            Speak();
        }
        private async void GetImageDataFromFile(MediaFile file)
        {
            theActivityIndicator.IsRunning = true;
            try
            {
                var result = await GetImageDescription(file.GetStream());

                ProcessImageResults(result);

            }
            catch (ComputerVisionErrorException ex)
            {
                theResults.Text = ex.Message;
            }
            theActivityIndicator.IsRunning = false;
        }

        //Speak Method to read text of results
        private async void Speak()
        {
            //if there are results then read the text and create a cancel token.
            if (theResults != null)
            {
                cts = new CancellationTokenSource();

                await TextToSpeech.SpeakAsync(theResults.Text, cancelToken: cts.Token);
            }

        }

        //Cancel Speech method to stop the text to speech when a new result is displayed 
        public void CancelSpeech()
        {
            if (cts?.IsCancellationRequested ?? true)
                return;

            cts.Cancel();

        }
        #endregion

    }

}
