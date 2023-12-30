# libraries
library(tidyverse)
library(ggplot2)
library(ggdist)
library(cmdstanr)
library(bayesplot)
library(posterior)
library(mcmcse)
library(HDInterval)

# data prep --------------------------------------------------------------------
data <- read.csv("ratings_final.csv", stringsAsFactors = TRUE)
head(data)

# means
mean(data$rating[data$serving_strategy == "gpt"])
median(data$rating[data$serving_strategy == "gpt"])
mean(data$rating[data$serving_strategy == "random"])
median(data$rating[data$serving_strategy == "random"])

# replace the value gpt with LLM
data$serving_strategy <- ifelse(data$serving_strategy == "gpt", "LLM", data$serving_strategy)

# replace the value random with traditional
data$serving_strategy <- ifelse(data$serving_strategy == "2", "traditional", data$serving_strategy)

# histograms
ggplot(data, aes(x = rating)) +
    geom_histogram(bins = 5) +
    facet_grid(serving_strategy ~ .) +
    ylab("") +
    xlab("Rating")

# save in a 1920x1080 pixel png file
ggsave("ratings_histogram.png", width = 1920, height = 1080, dpi = 300, units = "px")

# encode gpt as 1 and random as 0
x <- ifelse(data$serving_strategy == "gpt", 1, 0)

# dependent variable
y <- data$rating

# stan -------------------------------------------------------------------------
# model
model <- cmdstan_model("ordered.stan")

# data
stan_data <- list(n = nrow(data), k = 5, x = x, y = y)

# fit
fit <- model$sample(
    data = stan_data,
    parallel_chains = 4
)

# diagnostics ------------------------------------------------------------------
# traceplot for beta parameters
mcmc_trace(fit$draws("beta"))
mcmc_trace(fit$draws("c"))

# summary of betas
fit$summary("beta")
fit$summary("c")

# analysis ---------------------------------------------------------------------
# extract parameters
df_beta <- as_draws_df(fit$draws("beta"))
df_beta <- df_beta %>% select(-.chain, -.iteration, -.draw)
df_cutpoints <- as_draws_df(fit$draws("c"))
df_cutpoints <- df_cutpoints %>% select(-.chain, -.iteration, -.draw)

# plot betas -------------------------------------------------------------------
# rename for ease of addressing
colnames(df_beta) <- c("GPT")
mcse(df_beta$GPT > 0) # P = 0.99 +/- 0.01

# to long format
df_beta_long <- df_beta %>% gather(Beta, Value)

# plot
ggplot(data = df_beta_long, aes(x = Value, y = Beta)) +
    stat_eye(fill = "skyblue", alpha = 0.75) +
    xlim(-1.6, 1.6)

# save in a 1920x1080 pixel png file
ggsave("ratings.png", width = 1920, height = 1080, dpi = 300, units = "px")
