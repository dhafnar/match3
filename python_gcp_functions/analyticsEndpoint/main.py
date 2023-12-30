import pandas as pd
import pandas_gbq


def main(request):
    data = request.get_json()
    data_dict = {
        'created_date': [pd.to_datetime(data['createdDate'])],
        'user_id': [data['userId']],
        'level_uid': [data['levelGuid']],
        'current_level': [data['currentLevel']],
        'screen_name': [data['screenName']],
        'world_served': [data['worldServed']],
        'game_version': [int(data.get('gameVersion', 0))]
    }
    df_logs = pd.DataFrame(data_dict)
    pandas_gbq.to_gbq(df_logs, "match3.tracking", if_exists='append')
    return "ok"
