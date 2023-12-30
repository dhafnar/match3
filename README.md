# Match 3 project repository

This repository contains the code and data to accompany a research paper. 

It includes several components, each organized into its own folder. Below is an overview of each folder and its contents.

## python_player_type_comparison

This folder contains Python scripts used for comparing different player types. 

It also includes the data and exported visuals.

Python version is 3.11, dependencies are listed in requirements.txt.

## r_results_analysis

Contains R scripts used in the analysis. Data is also included in the folder.

## python_gcp_functions

Includes Python code designed for deployment as Google Cloud Platform (GCP) functions.

Instructions on how to deploy a function can be found here:
https://cloud.google.com/functions/docs/create-deploy-http-python-1st-gen

Each subfolder represents a separate function. Dependencies are included in requirements.txt for each function.

Python versions used:
- levelStatsEndpoint - Python 3.9
- analyticsEndpoint - Python 3.11
- genDefaultLevelsEndpoint - Python 3.11

Note that you must provide OpenAI API Key as OPENAI_API_KEY environmental variable for levelStatsEndpoint and genDefaultLevelsEndpoint to work.

## unity_match3game

This is the Unity code for the game which can be built to work on Android.

The code in this folder is an upgrade and a modification of a template "Game Academy: Make a Match Three Game in Unity", which can be found at [Game Academy Tutorial](https://gameacademy.school/project/match-three/).

To connect the application to the backend, you need to edit the paths in Assets\Scripts\GameManager.cs so that the application points to your backend functions and bucket.

Once you set up the project, install Firebase and Crashlytics plugins in Unity.

## bq_tables

This folder includes the list and schemas of Google BigQuery tables.

Each file is its own table. The contents of the .json file represent a schema of the table.

You can learn more about creating tables in BigQuery here: https://cloud.google.com/bigquery/docs/tables
