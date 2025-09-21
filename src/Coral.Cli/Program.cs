// See https://aka.ms/new-console-template for more information

using Coral.Services;

var trackPath = @"P:\Music\Fox - Squang Dangs in the Key of Vibes (2021) [NQ026] [WEB FLAC]\03 - Fox - Sunshine Blues (feat. Satl & [ K S R ]).flac";

var results = await InferenceService.RunInference(trackPath);
Console.WriteLine(string.Join(", ", results.Take(5)));