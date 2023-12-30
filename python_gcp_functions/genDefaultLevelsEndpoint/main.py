import pandas as pd
import pandas_gbq
from google.cloud import storage
import openai
import random
import json
from datetime import datetime
import uuid
import os

openai.api_key = os.getenv('OPENAI_API_KEY')

NUM_LEVELS_TO_SUGGEST = 3

# Define ranges as global variables
NUM_DIFFERENT_PIECES_RANGE = (3, 5)
NUM_DIFFERENT_GOALS = (2, 4)
SCORE_GOAL_RANGE = (700, 2000)
NUM_MOVES_RANGE = (20, 30)
BOARD_SIZE_RANGE = (4, 6)
COLLECTION_GOALS_PIECES_RANGE = (5, 15)

filename_random = "000_random_default"
filename_gpt = "000_gpt_default"


def main(request):
    data = request.get_json()
    if data['levelsServingStrategy'] == "gpt":
        response = call_to_openai()
        levels = add_uid_and_date(response['levels'])
        levels_to_bucket(filename_gpt, levels)
        pass
    else:
        levels = add_uid_and_date(generate_random_levels())
        levels_to_bucket(filename_random, levels)
    return levels


json_function = """
[
{
  "name": "next_levels",
  "description": "Specify parameters for the next levels. Takes an array of levels.",
  "parameters": {
    "type": "object",
    "properties": {
      "levels": {
        "level_number": {
          "type": "number",
          "description": "Which level in a row."
        },
        "num_different_pieces": {
          "type": "number",
          "description": "Amount of different pieces in the level."
        },
        "score_goal": {
          "type": "number",
          "description": "Score goal for the level."
        },
        "num_moves": {
          "type": "number",
          "description": "Number of moves in which the level needs to be completed."
        },
        "board_width": {
          "type": "number",
          "description": "Width of the board."
        },
        "board_height": {
          "type": "number",
          "description": "Height of the board."
        },
        "collection_goals": {
          "type": "array",
          "items": {
            "type": "number",
            "description": "Number of pieces of a certain color to collect to complete the game. Each item is different color piece. Example: [30, 20]"
          }
        }
      }
    }
  }
}
]
"""

system_prompt = """
You are a system that predicts what types of levels a player would prefer given that they are playing this game for the first time.
"""

user_prompt = f"""
LEVEL_GENERATION_PARAMETERS are:
level_number: A number of the current level.
num_different_pieces: More different pieces, harder the game. Valid range {NUM_DIFFERENT_PIECES_RANGE}.
score_goal: The score a user must reach before completing the level. The score should be divisable by 3. Valid range {SCORE_GOAL_RANGE}.
num_moves - Amount of moves a user has to complete the game. Harder levels need more moves: consider collection_goals. Usually number is {NUM_MOVES_RANGE}.
board_width - How wide the board is. Wider is harder. Valid range {BOARD_SIZE_RANGE}.
board_height - Height of the board. Higher is harder. Should be very similar to board-width. Valid range {BOARD_SIZE_RANGE}.
collection_goals - To finish the level, you need to collect a certain number of pieces with a specific color. 
The game involves {NUM_DIFFERENT_GOALS} unique colors, each represented by a number of pieces between {COLLECTION_GOALS_PIECES_RANGE}. 
For instance, if the game requires two different colors, you may need to collect 10 pieces of one color and 20 pieces of another, represented as [10, 20].

Your task is to suggest {NUM_LEVELS_TO_SUGGEST} levels of a game to a player that is completely new to it and starts with level 1.

Return the response in JSON format.
"""


def generate_random_levels():
    levels = []
    for _ in range(NUM_LEVELS_TO_SUGGEST):
        num_different_pieces = random.randint(*NUM_DIFFERENT_PIECES_RANGE)
        num_different_goals = random.randint(NUM_DIFFERENT_GOALS[0], min(NUM_DIFFERENT_GOALS[1], num_different_pieces))
        score_goal = random.choice([i for i in range(*SCORE_GOAL_RANGE) if i % 3 == 0])
        num_moves = random.randint(*NUM_MOVES_RANGE)
        board_width = random.randint(*BOARD_SIZE_RANGE)
        board_height = random.randint(*BOARD_SIZE_RANGE)
        collection_goals = [random.randint(*COLLECTION_GOALS_PIECES_RANGE) for _ in range(num_different_goals)]
        levels.append({
            "num_different_pieces": num_different_pieces,
            "score_goal": score_goal,
            "board_width": board_width,
            "board_height": board_height,
            "num_moves": num_moves,
            "time_seconds": 0,
            "collection_goals": collection_goals
        })
    return levels


def levels_to_bucket(default_name, json_levels):
    # Ensure json_levels is a Python object, not a string
    if isinstance(json_levels, str):
        json_levels = json.loads(json_levels)

    client = storage.Client()
    bucket = client.get_bucket('m3-levels')
    blob = bucket.blob(f'{default_name}.json')
    blob.cache_control = "public, max-age=0"
    formatted_json_data = json.dumps(json_levels, indent=4)

    blob.upload_from_string(
        data=formatted_json_data,
        content_type='application/json'
    )


def call_to_openai():
    functions = json.loads(json_function)

    _response = openai.ChatCompletion.create(
        model="gpt-4-0613",
        temperature=0,
        messages=[
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_prompt}],
        functions=functions,
        function_call={"name": "next_levels"}
    )
    response_json = json.loads(_response['choices'][0]['message']['function_call']['arguments'])

    return response_json


def add_uid_and_date(levels):
    for i in range(len(levels)):
        guid = "LUID-" + str(uuid.uuid4())
        dt = int(datetime.now().timestamp() * 1e6)

        levels[i] = {
            'level_uid': guid,
            'num_different_pieces': levels[i]['num_different_pieces'],
            'score_goal': levels[i]['score_goal'],
            'board_width': levels[i]['board_width'],
            'board_height': levels[i]['board_height'],
            'num_moves': levels[i]['num_moves'],
            'time_seconds': 0,  # never used in this test
            'created_time': dt,
            'collection_goals': levels[i]['collection_goals']
        }
    df_levels = pd.DataFrame(levels)
    pandas_gbq.to_gbq(df_levels, 'match3.level_params', if_exists='append')
    return json.dumps(levels)
