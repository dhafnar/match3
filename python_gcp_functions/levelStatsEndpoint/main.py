from datetime import datetime
from google.cloud import bigquery
import pandas as pd
import pandas_gbq
import openai
import json
import uuid
from google.cloud import storage
import logging
import numpy as np
import random
import time
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

FLAG_RANDOM = "Data was randomly generated"


def main(request):
    start_time = time.time()
    # We get the level data
    data = request.get_json()
    # We extract the level's GUID and LevelServingStrategy to use in different functions
    level_compl_id = data["level"]["levelGuid"]
    level_serv_strat = data["level"]["worldServed"]
    # The data on the level completion is sent to BQ with the unique id
    user_id = level_to_bq(data, level_compl_id)
    # Data on individual moves is saved to BQ (not used currently though)
    moves_to_bq(data, level_compl_id)
    # We determine the strategy for level generation for testing purposes
    if level_serv_strat == "gpt":
        # Player's stats are listed against the level requirements
        descriptions = get_stats_from_bigquery(user_id, False)
        logging.info(f"{user_id} - {level_serv_strat} Calling OpenAI ...")
        response_json = call_to_openai(descriptions)
    elif level_serv_strat == "gpt-stats":
        # This level's stats are compared against the stats for levels in the similar cluster
        descriptions = get_stats_from_bigquery(user_id, True)
        logging.info(f"{user_id} - {level_serv_strat} Calling OpenAI ...")
        response_json = call_to_openai(descriptions)
    else:
        descriptions = FLAG_RANDOM
        response_json = generate_response_for_random()

    # Levels are asigned unique IDs and their parameters writen to BQ for future reference
    json_levels = levels_to_bq(response_json['levels'])
    # Levels are finally written to bucket as a JSON file for the app to fetch them
    levels_to_bucket(user_id, json_levels)
    # Saves the data for further analysis
    log_all_data(user_id, level_compl_id, descriptions, response_json, start_time, data)
    return response_json


system_prompt = """
You are a system that predicts what types of levels a player would prefer based on their data.
"""

user_prompt = f"""
LEVEL_COMPLETITION_PARAMETERS are:
current_level - The level the data in the row refers to
level_passed - Was the level completed successfully?
score - Score earned in this attempt at the level. Higher is better.
moves_left - The number of moves left to complete the level. More is better.
num_failed_moves - Number of illegal moves (the pieces moved back as no match was made). Zero for great players.
num_clicks_on_board - Number of overall clicks on the board (for any reason). Lower is more efficient.


TYPE_OF_GAMER options are: not so skilled player, casual player, great player

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

Your task is to:
1. Consider the DATA_ON_THE_PLAYER
2. Consider parameters of the levels player already completed.
3. Determine what type of player we are dealing with based on TYPE_OF_GAMER. Mostly consider level of skill, fun vs. complex, puzzly vs arcade.
4. Suggest {NUM_LEVELS_TO_SUGGEST} next levels for this player based on the TYPE_OF_GAMER and the list of LEVEL_COMPLETITION_PARAMETERS.
5. Explain your reasoning for TYPE_OF_GAMER and next {NUM_LEVELS_TO_SUGGEST} levels

Return the response in JSON format.
"""

json_function = """
[
{
  "name": "next_levels",
  "description": "Specify parameters for the next levels. Takes an array of levels.",
  "parameters": {
    "type": "object",
    "properties": {
      "player_type": {
        "type": "string",
        "description": "Type of the player from a predefined list."
      },
      "type_explanation": {
        "type": "string",
        "description": "Reasoning behind the player_type assignment and selection of the parameters for the next levels."
      },
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


def log_all_data(user_id, level_compl_id, descriptions, response_json, start_time, data):
    current_datetime = pd.to_datetime(datetime.now())
    player_type = response_json.get('player_type', '')
    type_explanation = response_json.get('type_explanation', '')
    levels = json.dumps(response_json.get('levels', {}))
    differences = compare_all_levels(response_json.get('levels', {}))
    execution_seconds = int(time.time() - start_time)
    game_version = int(data["level"].get("gameVersion", 0))

    data_dict = {
        'date': [current_datetime],
        'user_id': [user_id],
        'level_compl_id': [level_compl_id],
        'description': [descriptions],
        'resp_player_type': [player_type],
        'resp_type_explanation': [type_explanation],
        'resp_type_levels': [levels],
        'differences': [differences],
        'execution_seconds': [execution_seconds],
        'game_version': [game_version]
    }

    df_logs = pd.DataFrame(data_dict)
    pandas_gbq.to_gbq(df_logs, "match3.logs", if_exists='append')
    logging.info(f"{user_id} - {player_type} Writing logs ...")


def level_to_bq(data, level_compl_id):
    level = data["level"]

    user_id = level["userId"]
    current_level = int(level["currentLevel"])
    level_passed = level["isLevelPassed"]
    score = int(level["score"])
    moves_left = int(level["movesLeft"])
    num_failed_moves = int(level["numFailedMoves"])
    date = datetime.strptime(level["dateSent"], '%Y-%m-%d %H:%M:%S')
    device_model = level["deviceModel"]
    timePlaying = float(level["timePlaying"])
    num_clicks_on_board = level["numBoardClicksOverall"]
    user_rating = level["userRating"]
    world_serverd = level["worldServed"]
    num_boosters_used = level["numBoostersUsed"]
    game_version = int(level.get("gameVersion", 0))
    function_version = int(os.environ.get('K_REVISION', 0))

    # Instantiates a client
    bigquery_client = bigquery.Client()

    # Prepares a reference to the dataset
    dataset_ref = bigquery_client.dataset('match3')

    table_ref = dataset_ref.table('player_data')
    table = bigquery_client.get_table(table_ref)  # API call

    rows_to_insert = [
        (user_id, current_level, level_passed, score, moves_left, num_failed_moves, date, device_model, timePlaying,
         num_clicks_on_board, user_rating, world_serverd, level_compl_id, num_boosters_used, game_version,
         function_version)
    ]

    errors = bigquery_client.insert_rows(table, rows_to_insert)  # API request
    assert errors == []

    return user_id


def moves_to_bq(data, level_compl_id):
    if not data['moves']:
        logging.warning("No data for moves, skipping.")
        return

    df_moves = pd.DataFrame.from_records(data['moves'])
    df_moves['moveNumber'] = df_moves['moveNumber'].astype('int64')
    df_moves['durationInSeconds'] = df_moves['durationInSeconds'].astype('float')
    df_moves['isMoveLegal'] = df_moves['isMoveLegal'].astype('bool')
    df_moves['scoreForMove'] = df_moves['scoreForMove'].astype('int64')
    df_moves['starsForMove'] = df_moves['starsForMove'].astype('int64')
    df_moves['levelCompletitionId'] = level_compl_id
    df_moves['createTime'] = pd.to_datetime(df_moves['createTime'], format='%Y-%m-%d %H:%M:%S')

    pandas_gbq.to_gbq(df_moves, "match3.moves", if_exists='append')


def handle_collection_goals(collection_goals):
    if isinstance(collection_goals, list):
        cleaned_goals = [int(goal) for goal in collection_goals if not np.isnan(goal)]
        return f"Collection goals were {cleaned_goals}."
    else:
        return ""


def generate_sentence_per_row(row):
    collection_goals_sentence = handle_collection_goals(row['collection_goals'])

    sentence = f"For level {row['level_in_row']}, the user scored {row['score']} where {row['score_goal']} " \
               f"was the minimum to pass. They had {row['moves_left']} moves left out of {row['num_moves']}. " \
               f"They made {row['num_failed_moves']} failed moves. " \
               f"They made {row['num_clicks_on_board']} clicks on the board. " \
               f"They used {row['num_boosters_used']} boosters." \
               f"Player rated the level as {row['user_rating']} out of 5. " \
               f"The level contained {row['num_different_pieces']} different pieces. " \
               f"Board width x height was {row['board_width']} x {row['board_height']}. " \
               f"{collection_goals_sentence}"
    print(sentence)
    return sentence


def generate_sentence_per_row_w_stats(row):
    collection_goals_sentence = handle_collection_goals(row['collection_goals'])

    sentence = f"For level {row['level_in_row']} " f"the user scored {row['score']} compared to an average score of {row['avg_score']}" \
               f" (median: {row['median_score']}, min: {row['min_score']}, max: {row['max_score']}). " \
               f"They had {row['moves_left']} moves left compared to an average of " \
               f"{row['avg_moves_left']} (median: {row['median_moves_left']}, min: {row['min_moves_left']}, " \
               f"max: {row['max_moves_left']}). They made {row['num_failed_moves']} failed moves compared to an average of " \
               f"{row['avg_num_failed_moves']} (median: {row['median_num_failed_moves']}, min: {row['min_num_failed_moves']}, " \
               f"max: {row['max_num_failed_moves']}). They made {row['num_clicks_on_board']} clicks on the board compared to an average of " \
               f"{row['avg_num_clicks_on_board']} (median: {row['median_num_clicks_on_board']}, min: {row['min_num_clicks_on_board']}, " \
               f"max: {row['max_num_clicks_on_board']}). They used {row['num_boosters_used']} boosters compared to an average of " \
               f"{row['avg_num_boosters_used']} (median: {row['median_num_boosters_used']}, min: {row['min_num_boosters_used']}, " \
               f"max: {row['max_num_boosters_used']}). Player rated the level as {row['user_rating']} out of 5. The level contained " \
               f"{row['num_different_pieces']} different pieces, the passing score was {row['score_goal']}, " \
               f"Board width x height was {row['board_width']} x {row['board_height']} and the allowed nuber of moves was {row['num_moves']}." \
               f"{collection_goals_sentence}"
    return sentence


def generate_descriptions(df, use_stats):
    if use_stats:
        df['description'] = df.apply(
            lambda row: generate_sentence_per_row(row) if row['max_score'] == 0
            else generate_sentence_per_row_w_stats(row), axis=1)
    else:
        df['description'] = df.apply(generate_sentence_per_row, axis=1)
    return '\n'.join(df['description'])


def get_stats_from_bigquery(_user_id, use_stats):
    df = pandas_gbq.read_gbq(f"SELECT * FROM match3.player_levels('{_user_id}')")
    return generate_descriptions(df, use_stats)


def call_to_openai(data_on_the_player):
    functions = json.loads(json_function)
    _response = openai.ChatCompletion.create(
        model="gpt-4-0613",
        temperature=0,
        messages=[
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": data_on_the_player + user_prompt}],
        functions=functions,
        function_call={"name": "next_levels"}
    )
    response_json = json.loads(_response['choices'][0]['message']['function_call']['arguments'])

    return response_json


def levels_to_bq(levels):
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
            'time_seconds': 0,  # TODO utilize
            'created_time': dt,
            'collection_goals': levels[i]['collection_goals']
        }

    df_levels = pd.DataFrame(levels)
    pandas_gbq.to_gbq(df_levels, 'match3.level_params', if_exists='append')
    return json.dumps(levels)


def levels_to_bucket(user_id, json_levels):
    # Ensure json_levels is a Python object, not a string
    if isinstance(json_levels, str):
        json_levels = json.loads(json_levels)

    client = storage.Client()
    bucket = client.get_bucket('m3-levels')
    blob = bucket.blob(f'{user_id}.json')
    blob.cache_control = "public, max-age=0"
    formatted_json_data = json.dumps(json_levels, indent=4)

    try:
        blob.upload_from_string(
            data=formatted_json_data,
            content_type='application/json'
        )
        logging.info(f'https://storage.googleapis.com/m3-levels/{user_id}.json uploaded.')
    except Exception as e:
        logging.error(f"An error occurred while uploading to bucket: {e}")


def compare_all_levels(levels):
    # Initialize variables to store the previous values
    prev_board_size = None
    prev_num_pieces = None
    prev_score_goal = None
    prev_collection_goals = None
    prev_num_moves = None

    # Function to compare the current and previous values and generate a sentence
    def compare_levels(level, level_number, prev_board_size, prev_num_pieces, prev_score_goal, prev_collection_goals,
                       prev_num_moves):
        sentences = ""
        if level_number > 1:
            sentences = [f"Changes in level {level_number}:"]

        # Compare the board size
        board_size = (level['board_width'], level['board_height'])
        if prev_board_size and board_size != prev_board_size:
            sentences.append(f"The board size changed from {prev_board_size} to {board_size}.")

        # Compare the number of different pieces
        num_pieces = level['num_different_pieces']
        if prev_num_pieces and num_pieces != prev_num_pieces:
            diff_pieces = num_pieces - prev_num_pieces
            sentences.append(
                f"The number of different pieces changed from {prev_num_pieces} to {num_pieces} (difference: {diff_pieces}).")

        # Compare the score goal
        score_goal = level['score_goal']
        if prev_score_goal and score_goal != prev_score_goal:
            diff_score = score_goal - prev_score_goal
            sentences.append(
                f"The score goal changed from {prev_score_goal} to {score_goal} (difference: {diff_score}).")

        # Compare the collection goals
        collection_goals = level['collection_goals']
        if prev_collection_goals and collection_goals != prev_collection_goals:
            sentences.append(f"The collection goals changed from {prev_collection_goals} to {collection_goals}.")

        # Compare the number of moves
        num_moves = level['num_moves']
        if prev_num_moves and num_moves != prev_num_moves:
            diff_moves = num_moves - prev_num_moves
            sentences.append(
                f"The number of moves changed from {prev_num_moves} to {num_moves} (difference: {diff_moves}).")

        return ' '.join(sentences), board_size, num_pieces, score_goal, collection_goals, num_moves

    all_sentences = ""
    # Compare each level with the previous one
    for i, level in enumerate(levels, start=1):
        sentence, prev_board_size, prev_num_pieces, prev_score_goal, prev_collection_goals, prev_num_moves = compare_levels(
            level, i, prev_board_size, prev_num_pieces, prev_score_goal, prev_collection_goals, prev_num_moves)
        if sentence:
            all_sentences += sentence + "\n"
    return all_sentences


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


def generate_response_for_random():
    levels = generate_random_levels()
    data = {
        "player_type": FLAG_RANDOM,
        "type_explanation": FLAG_RANDOM,
        "levels": levels
    }
    return data
