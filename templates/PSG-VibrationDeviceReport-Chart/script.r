source('./r_files/flatten_HTML.r')

############### Library Declarations ###############
libraryRequireInstall("ggplot2");
libraryRequireInstall("plotly");
libraryRequireInstall("dplyr");
libraryRequireInstall("reshape2");
####################################################

################### Actual code ####################
 
# Reformatting the data so it can be used as a plotly surface plot
data_z <- acast(Values, Frequency~Timestamp, value.var='Magnitude')
 
# Plotting the surface plot
g <- plot_ly(z = data_z, type='surface') %>%
	  layout(
		scene = list(
		  xaxis = list(title = "Timestamp"),
		  yaxis = list(title = "Frequency"),
		  zaxis = list(title = "Magnitude")
		))

############# Create and save widget ###############
internalSaveWidget(g, 'out.html');
####################################################
