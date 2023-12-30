import pandas as pd
from plotnine import ggplot, aes, geom_bar, geom_errorbar, labs, theme, element_text, position_dodge

data = pd.read_csv('ratings.csv')

average_rating = data.groupby('serving_strategy')['rating'].mean()
standard_deviation = data.groupby('serving_strategy')['rating'].std()
standard_error = data.groupby('serving_strategy')['rating'].sem()
total_sample_size = data['rating'].count()

cat_gpt = "LLM"
cat_random = "Traditional PCG"

data_ordered = {
    'category': [cat_gpt, cat_random],
    'value': [average_rating['gpt'], average_rating['random']],
    'std_err': [standard_error['gpt'], standard_error['random']]
}
df = pd.DataFrame(data_ordered)

plot = (ggplot(df, aes(x='category', y='value', fill='category')) +
        geom_bar(stat='identity', position='dodge') +
        geom_errorbar(aes(ymin='value-std_err', ymax='value+std_err'),
                      position=position_dodge(width=0.9), width=0.25) +
        labs(y='Value', x='Category') +
        theme(figure_size=(10, 6), legend_position='none',
              text=element_text(size=16),
              axis_title=element_text(size=18),
              axis_text=element_text(size=14),
              strip_text=element_text(size=16),
              plot_title=element_text(size=20))
        )

print(plot)
plot.save("ratings.png", dpi=300)
