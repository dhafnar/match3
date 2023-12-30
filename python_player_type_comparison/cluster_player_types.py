import pandas as pd
from sklearn.cluster import KMeans
from sklearn.preprocessing import StandardScaler, MinMaxScaler
from plotnine import ggplot, aes, geom_point, labs, theme, facet_wrap, element_text
from sklearn.metrics import adjusted_rand_score
from sklearn.manifold import TSNE
import os
import pickle

random = "traditional PCG"

original_df = pd.read_csv('generated_levels.csv')
original_df['resp_player_type'] = original_df['resp_player_type'].replace('Data was randomly generated', random)


def process_data(version):
    if version == "all":
        df = original_df
    if version == "no random":
        df = original_df[original_df['resp_player_type'] != random]

    if version == "two groups":
        df = original_df[original_df['resp_player_type'] != random]
        df['resp_player_type'] = original_df['resp_player_type'].replace(['casual player', 'not so skilled player'],
                                                                         'not great player')

    if version == "two + random":
        df = original_df
        df['resp_player_type'] = original_df['resp_player_type'].replace(['casual player', 'not so skilled player'],
                                                                         'not great player')

    # Step 2: Select numeric columns for clustering
    numeric_columns = ["level_1_num_different_pieces", "level_1_score_goal", "level_1_board_width",
                       "level_1_board_height",
                       "level_1_num_moves", "level_1_sum_collection_goals", "level_2_num_different_pieces",
                       "level_2_score_goal", "level_2_board_width", "level_2_board_height", "level_2_num_moves",
                       "level_2_sum_collection_goals", "level_3_num_different_pieces", "level_3_score_goal",
                       "level_3_board_width", "level_3_board_height", "level_3_num_moves",
                       "level_3_sum_collection_goals"]

    df[numeric_columns].to_csv('numeric_data.csv', index=False)

    # Normalize the numeric columns
    scaler = StandardScaler()
    normalized_data = scaler.fit_transform(df[numeric_columns])

    # Find the number of unique player types
    n_clusters = df['resp_player_type'].nunique()

    # Now use n_clusters for KMeans clustering
    kmeans = KMeans(n_clusters=n_clusters, n_init=1000)

    cluster_assignments = kmeans.fit_predict(normalized_data)

    # Assign clusters to the corresponding rows in the DataFrame
    df.loc[:, 'cluster'] = cluster_assignments

    player_type_mapping = {ptype: i for i, ptype in enumerate(df['resp_player_type'].unique())}

    # Apply t-SNE to reduce the dimensionality to 2D
    tsne = TSNE(n_components=2, random_state=42)
    tsne_results = tsne.fit_transform(normalized_data)

    scaler = MinMaxScaler(feature_range=(-100, 100))
    tsne_scaled = scaler.fit_transform(tsne_results)

    df.loc[:, ['tsne_1', 'tsne_2']] = tsne_scaled

    return df


def load_or_process_data(version):
    pickle_file = f'dumps/clusters_{version}.pkl'
    if os.path.exists(pickle_file):
        with open(pickle_file, 'rb') as f:
            data = pickle.load(f)
        print(f"Data loaded from {pickle_file}")
    else:
        data = process_data(version)
        with open(pickle_file, 'wb') as f:
            pickle.dump(data, f)
        print(f"Data processed and saved to {pickle_file}")
    return data


def create_tsne_dataframe(tsne_results, player_type_integers, player_type_mapping):
    inverted_player_type_mapping = {v: k for k, v in player_type_mapping.items()}
    df_tsne = pd.DataFrame(tsne_results, columns=['x', 'y'])
    df_tsne['player_type'] = player_type_integers.map(inverted_player_type_mapping)
    return df_tsne


df_v1 = load_or_process_data("no random")
df_v2 = load_or_process_data("all")


def prepare_scatterplot(_df):
    return (ggplot(_df, aes(x='tsne_1', y='tsne_2', color='resp_player_type')) +
            geom_point(size=3) +
            labs(color='Player type', x='t-SNE Component 1', y='t-SNE Component 2') +
            theme(legend_title_align='left',
                  aspect_ratio=0.6,
                  figure_size=(10, 3),
                  strip_text=element_text(size=9))
            + facet_wrap('~version', ncol=2)
            )


def get_title(_df, _prefix):
    _n = str(_df.shape[0])
    _ari_score = round(adjusted_rand_score(_df['resp_player_type'], _df['cluster']), 2)
    return f"{_prefix} (n:{_n}, ARI: {_ari_score})"


df_v1['version'] = 'Assigned Player Types'
df_v2['version'] = 'Player Types + Traditional PCG'

combined_df = pd.concat([df_v1, df_v2])

plot = prepare_scatterplot(combined_df)
print(plot)
plot.save("tsne_2_versions.png", dpi=300)
